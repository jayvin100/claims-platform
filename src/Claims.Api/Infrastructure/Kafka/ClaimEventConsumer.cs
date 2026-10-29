using System.Text;
using System.Text.Json;
using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Outbox;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Infrastructure.Kafka;

/// <summary>
/// Reads claim-events for one consumer (Notifications or Reporting) with the delivery rules both need
/// (ADR-0004, ADR-0005, D16): its own consumer group; one message at a time; event ids it has already handled
/// are skipped; the processed id is saved in the same transaction as the consumer's own rows; the Kafka offset
/// is committed only after that; and a message that fails is retried, never skipped. The one exception is a poison
/// message, which no retry could ever fix: it is copied to the dead-letter topic and the consumer moves on (D90).
/// </summary>
public abstract class ClaimEventConsumer(
    IServiceScopeFactory scopes, KafkaOptions kafka, TimeProvider time, ILogger logger) : BackgroundService
{
    /// <summary>The consumer group id, and the name its processed event ids are recorded under.</summary>
    protected abstract string ConsumerName { get; }

    /// <summary>
    /// Adds this consumer's rows for one event to <paramref name="db"/> without saving them. The caller saves
    /// them together with the processed event id, so both are stored or neither is. Returns anything that must
    /// happen only once they are saved (such as a console line), or null; the caller runs it after the save.
    /// Throws <see cref="PoisonMessageException"/> for an event type the consumer doesn't handle.
    /// </summary>
    protected abstract Task<Action?> ApplyAsync(
        AppDbContext db, ClaimEvent claimEvent, DateTimeOffset now, CancellationToken stoppingToken);

    /// <summary>
    /// Every claim event type, by the name in the Kafka header (the class name, see OutboxMessage.From), so the JSON
    /// can be read into the right type.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Type> EventTypes = typeof(ClaimEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(ClaimEvent)))
        .ToDictionary(t => t.Name);

    /// <summary>
    /// The outbox's JSON settings, made strict for reading: a missing field, or a null in a field that can't be null,
    /// fails instead of quietly becoming an empty Guid or a null. So such an event goes to the dead-letter topic,
    /// rather than writing a bogus email or failing the database save and retrying for ever. Enums must be names, so
    /// "market": 99 fails here too; that rule comes with the outbox's settings (OutboxMessage.JsonOptions).
    /// </summary>
    private static readonly JsonSerializerOptions ReadOptions = new(OutboxMessage.JsonOptions)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    // .NET 10 runs ExecuteAsync on a background thread, so the blocking Consume call doesn't hold up startup.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig(kafka.ClientConfig())
        {
            GroupId = ConsumerName,
            // Offsets move only when we commit them, after the database transaction (see below).
            EnableAutoCommit = false,
            // A group with no committed offset yet starts from the oldest message, so no event is missed.
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Check for new topics every 5 s (default 5 min), so a consumer that starts before the topic
            // creator has made claim-events starts reading soon after the topic appears.
            TopicMetadataRefreshIntervalMs = 5000,
        };
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaOptions.Topic);
        // Only used to send poison messages to the dead-letter topic.
        using var deadLetters = new ProducerBuilder<string, string>(kafka.ProducerConfig()).Build();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    await HandleAsync(result, deadLetters, stoppingToken);
                    // Only now is the event safely recorded (or dead-lettered), so only now does the group move past
                    // it. If we crash before this line, Kafka redelivers the message: HandleAsync skips an event it
                    // already recorded, and sends a poison message to the dead-letter topic again.
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // The host is stopping.
                }
                catch (Exception ex)
                {
                    // Anything else, including an OperationCanceledException from a timeout, is a failure that may
                    // pass: the database is down, or the dead-letter send failed. The same message is retried until
                    // it succeeds. That holds up the rest of its partition, by design: skipping it would lose the
                    // event, and moving past it would break the Claim's order.
                    logger.LogError(ex, "{Consumer} couldn't handle a message; retrying in {Delay}s",
                        ConsumerName, KafkaOptions.RetryDelay.TotalSeconds);
                    if (result is not null)
                        RewindTo(consumer, result);
                    await Task.Delay(KafkaOptions.RetryDelay, stoppingToken);
                }
            }
        }
        finally
        {
            // Leave the group cleanly, so Kafka hands the partitions on straight away.
            consumer.Close();
        }
    }

    // Without this, the next Consume would return the message after the failed one, which would then be
    // committed, silently skipping the failure. Seeking back makes the next Consume return it again.
    private void RewindTo(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
    {
        try
        {
            consumer.Seek(result.TopicPartitionOffset);
        }
        catch (KafkaException ex)
        {
            // The partition was handed elsewhere in a rebalance. Its next owner starts from the last
            // committed offset, which is still this message, so it isn't lost.
            logger.LogWarning(ex, "{Consumer} couldn't rewind to {Offset}", ConsumerName, result.TopicPartitionOffset);
        }
    }

    /// <summary>
    /// Handles one Kafka message: applies it, or sends it to the dead-letter topic if it is poison. Returns once it is
    /// safe to commit past the message; throws if it must be retried, including when the dead-letter send fails, so a
    /// message is never lost. Public so tests can pass a fake producer, with no Kafka.
    /// </summary>
    public async Task HandleAsync(
        ConsumeResult<string, string> result, IProducer<string, string> deadLetters, CancellationToken stoppingToken)
    {
        var message = result.Message;
        // A header can be present with a null value; that counts as no event type (poison), not as a crash.
        string? eventType = message.Headers is not null
            && message.Headers.TryGetLastBytes(KafkaOptions.EventTypeHeader, out var bytes) && bytes is not null
            ? Encoding.UTF8.GetString(bytes)
            : null;
        try
        {
            await ProcessAsync(eventType, message.Value, stoppingToken);
        }
        catch (PoisonMessageException ex)
        {
            await SendToDeadLetterTopicAsync(result, ex.Message, deadLetters, stoppingToken);
        }
    }

    // Copies the message as it arrived (key, value and headers) and adds who sent it here, why, and where it came
    // from, so a person can find the original and replay it. Nothing is recorded as processed, so a replayed copy
    // is handled like a new event. If the process dies after this send but before the commit, the message is
    // dead-lettered twice; both copies carry the same dlq-source.
    private async Task SendToDeadLetterTopicAsync(
        ConsumeResult<string, string> result, string reason, IProducer<string, string> deadLetters, CancellationToken stoppingToken)
    {
        var source = $"{result.Topic}/{result.Partition.Value}/{result.Offset.Value}";
        var headers = new Headers();
        foreach (var header in result.Message.Headers ?? [])
            headers.Add(header.Key, header.GetValueBytes());
        headers.Add(KafkaOptions.DeadLetterConsumerHeader, Encoding.UTF8.GetBytes(ConsumerName));
        headers.Add(KafkaOptions.DeadLetterReasonHeader, Encoding.UTF8.GetBytes(reason));
        headers.Add(KafkaOptions.DeadLetterSourceHeader, Encoding.UTF8.GetBytes(source));

        // Awaited, so the caller commits only once Kafka has stored the dead letter. A failed send throws, and the
        // message is retried in place like any other failure.
        await deadLetters.ProduceAsync(KafkaOptions.DeadLetterTopic, new Message<string, string>
        {
            Key = result.Message.Key,
            Value = result.Message.Value,
            Headers = headers,
        }, stoppingToken);
        logger.LogWarning("{Consumer} sent {Source} to {DeadLetterTopic}: {Reason}",
            ConsumerName, source, KafkaOptions.DeadLetterTopic, reason);
    }

    /// <summary>
    /// Reads a message into its event, or throws <see cref="PoisonMessageException"/> with the reason if it can't be
    /// read: no event type, an empty value, a type that isn't a claim event, or JSON that doesn't fit that type
    /// (including a value the database could never store, see CheckValues). Pure, with no database, so a database
    /// error can never be mistaken for poison. Public so tests read events exactly as the consumers do.
    /// </summary>
    public static ClaimEvent ReadEvent(string? eventType, string? eventJson)
    {
        if (string.IsNullOrEmpty(eventType))
            throw new PoisonMessageException("no event-type header");
        if (string.IsNullOrEmpty(eventJson))
            throw new PoisonMessageException("empty value");
        if (!EventTypes.TryGetValue(eventType, out var type))
            throw new PoisonMessageException($"unknown event type {eventType}");
        ClaimEvent claimEvent;
        try
        {
            // "null" is valid JSON, but not an event.
            claimEvent = (ClaimEvent?)JsonSerializer.Deserialize(eventJson, type, ReadOptions)
                ?? throw new PoisonMessageException($"{eventType} is null");
        }
        catch (JsonException ex)
        {
            // Not JSON, a missing field, a null where none is allowed, or text where a number should be.
            throw new PoisonMessageException($"can't read {eventType}: {ex.Message}");
        }
        CheckValues(eventType, claimEvent);
        return claimEvent;
    }

    // Valid JSON can still hold a value the database would refuse on every retry, or a Market that Reporting has no
    // currency for. That counts as JSON that doesn't fit, so it is poison too. The limits are the Claims table's
    // columns, where every real event's values come from. Not caught: a hand-made comma list of Markets reads as
    // flags, and can land on another, real Market ("HK, MY" is TH); that is stored without error.
    private static void CheckValues(string eventType, ClaimEvent claimEvent)
    {
        if (!Enum.IsDefined(claimEvent.Market))
            throw new PoisonMessageException($"{eventType} has an unknown market {claimEvent.Market}");
        // Notifications puts it in an email Subject (200 at most), which fits any claim number up to this length.
        if (claimEvent.ClaimNumber.Length > AppDbContext.ClaimNumberLength)
            throw new PoisonMessageException($"{eventType} has a claim number over {AppDbContext.ClaimNumberLength} characters");
        // Only these two events carry a currency.
        var currency = claimEvent switch
        {
            ClaimSubmitted e => e.Currency,
            ReserveSet e => e.Currency,
            _ => null,
        };
        if (currency is not null && currency.Length != AppDbContext.CurrencyLength)
            throw new PoisonMessageException($"{eventType} has a currency that isn't {AppDbContext.CurrencyLength} characters");
        // Postgres refuses a NUL character in text, so an event holding one would be retried for ever.
        if (claimEvent.GetType().GetProperties().Any(p => p.GetValue(claimEvent) is string s && s.Contains('\0')))
            throw new PoisonMessageException($"{eventType} has a NUL character");
    }

    /// <summary>
    /// Handles one event once: skips it if this consumer already recorded its id, otherwise applies it and records
    /// the id in one transaction. Throws <see cref="PoisonMessageException"/>, having saved nothing, for a message
    /// it can never handle. Public so tests can call it without Kafka.
    /// </summary>
    public async Task ProcessAsync(string? eventType, string? eventJson, CancellationToken stoppingToken)
    {
        // Read before the database, so a database error is never mistaken for poison: it still retries.
        var claimEvent = ReadEvent(eventType, eventJson);
        var eventId = claimEvent.EventId;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Delivery is at-least-once, so the same event can arrive twice; the second time does nothing.
        if (await db.ProcessedEvents.AnyAsync(p => p.Consumer == ConsumerName && p.EventId == eventId, stoppingToken))
        {
            logger.LogInformation("{Consumer} skipped {EventType} {EventId}: already processed", ConsumerName, eventType, eventId);
            return;
        }

        var now = time.GetUtcNow();
        var afterSave = await ApplyAsync(db, claimEvent, now, stoppingToken);
        db.ProcessedEvents.Add(new ProcessedEvent(ConsumerName, eventId, now));
        // One SaveChanges is one transaction: the consumer's rows and the processed id are stored together.
        await db.SaveChangesAsync(stoppingToken);
        // Only after the commit, and only for a new event: if the save fails, the retry would otherwise repeat it,
        // and a redelivered duplicate returns above before reaching here.
        afterSave?.Invoke();
    }
}
