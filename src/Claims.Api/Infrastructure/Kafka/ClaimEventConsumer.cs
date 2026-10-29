using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Claims.Api.Infrastructure.Data;
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
        AppDbContext db, Guid eventId, string eventType, string eventJson, DateTimeOffset now, CancellationToken stoppingToken);

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
                    // it. If we crash before this line, Kafka redelivers the message and HandleAsync skips it.
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
        string? eventType = message.Headers is not null && message.Headers.TryGetLastBytes(KafkaOptions.EventTypeHeader, out var bytes)
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
    /// Reads the event id, or throws <see cref="PoisonMessageException"/> with the reason if the message can't be
    /// decoded. [NotNull] tells the compiler both arguments are non-null once it returns.
    /// </summary>
    private static Guid ReadEventId([NotNull] string? eventType, [NotNull] string? eventJson)
    {
        if (string.IsNullOrEmpty(eventType))
            throw new PoisonMessageException("no event-type header");
        if (string.IsNullOrEmpty(eventJson))
            throw new PoisonMessageException("empty value");

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(eventJson);
        }
        catch (JsonException)
        {
            throw new PoisonMessageException("not JSON");
        }
        using (json)
        {
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("eventId", out var id)
                && id.ValueKind == JsonValueKind.String
                && id.TryGetGuid(out var eventId))
                return eventId;
            throw new PoisonMessageException("no event id");
        }
    }

    /// <summary>
    /// Handles one event once: skips it if this consumer already recorded its id, otherwise applies it and records
    /// the id in one transaction. Throws <see cref="PoisonMessageException"/>, having saved nothing, for a message
    /// it can never handle. Public so tests can call it without Kafka.
    /// </summary>
    public async Task ProcessAsync(string? eventType, string? eventJson, CancellationToken stoppingToken)
    {
        // Checked before the database, so a database error is never mistaken for poison: it still retries.
        var eventId = ReadEventId(eventType, eventJson);

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Delivery is at-least-once, so the same event can arrive twice; the second time does nothing.
        if (await db.ProcessedEvents.AnyAsync(p => p.Consumer == ConsumerName && p.EventId == eventId, stoppingToken))
        {
            logger.LogInformation("{Consumer} skipped {EventType} {EventId}: already processed", ConsumerName, eventType, eventId);
            return;
        }

        var now = time.GetUtcNow();
        Action? afterSave;
        try
        {
            afterSave = await ApplyAsync(db, eventId, eventType, eventJson, now, stoppingToken);
        }
        catch (JsonException)
        {
            // Valid JSON with an event id, but its fields don't fit its event type (say, text where a number should
            // be). No retry can fix that either.
            throw new PoisonMessageException($"JSON doesn't fit {eventType}");
        }
        db.ProcessedEvents.Add(new ProcessedEvent(ConsumerName, eventId, now));
        // One SaveChanges is one transaction: the consumer's rows and the processed id are stored together.
        await db.SaveChangesAsync(stoppingToken);
        // Only after the commit, and only for a new event: if the save fails, the retry would otherwise repeat it,
        // and a redelivered duplicate returns above before reaching here.
        afterSave?.Invoke();
    }
}
