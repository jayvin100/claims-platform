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
/// is committed only after that; and a message that fails is retried, never skipped (the one exception is an
/// unreadable message, see <see cref="SkipUnreadable"/>).
/// </summary>
public abstract class ClaimEventConsumer(
    IServiceScopeFactory scopes, KafkaOptions kafka, TimeProvider time, ILogger logger) : BackgroundService
{
    /// <summary>The consumer group id, and the name its processed event ids are recorded under.</summary>
    protected abstract string ConsumerName { get; }

    /// <summary>
    /// What to do with a message that can't be decoded (no event type, bad JSON, no event id). False (the default):
    /// throw, so it is retried like any failure and blocks its partition until fixed (D74). True: log a warning and
    /// move past it. Nothing is recorded, since there is no event id to record.
    /// </summary>
    protected virtual bool SkipUnreadable => false;

    /// <summary>
    /// Adds this consumer's rows for one event to <paramref name="db"/> without saving them. The caller saves
    /// them together with the processed event id, so both are stored or neither is. Returns anything that must
    /// happen only once they are saved (such as a console line), or null; the caller runs it after the save.
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

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    await HandleAsync(result.Message, stoppingToken);
                    // Only now is the event safely recorded, so only now does the group move past it.
                    // If we crash before this line, Kafka redelivers the message and HandleAsync skips it.
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // The host is stopping.
                }
                catch (Exception ex)
                {
                    // Anything else, including an OperationCanceledException from a timeout, is a failure.
                    // The same message is retried until it succeeds. That blocks the rest of its partition, by
                    // design: skipping it would lose the event, and moving past it would break the Claim's order.
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

    private Task HandleAsync(Message<string, string> message, CancellationToken stoppingToken)
    {
        string? eventType = message.Headers is not null && message.Headers.TryGetLastBytes(KafkaOptions.EventTypeHeader, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : null;
        return ProcessAsync(eventType, message.Value, stoppingToken);
    }

    /// <summary>Reads the event id, or returns false if the message can't be decoded at all.</summary>
    private static bool TryReadEventId(
        [NotNullWhen(true)] string? eventType, [NotNullWhen(true)] string? eventJson, out Guid eventId)
    {
        eventId = Guid.Empty;
        if (string.IsNullOrEmpty(eventType) || eventJson is null)
            return false;
        try
        {
            using var json = JsonDocument.Parse(eventJson);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("eventId", out var id)
                && id.ValueKind == JsonValueKind.String
                && id.TryGetGuid(out eventId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Handles one event once: skips it if this consumer already recorded its id, otherwise applies it and records
    /// the id in one transaction. Public so tests can call it without Kafka.
    /// </summary>
    public async Task ProcessAsync(string? eventType, string? eventJson, CancellationToken stoppingToken)
    {
        // Checked before the database, so only a decode failure can be skipped; database errors still retry.
        if (!TryReadEventId(eventType, eventJson, out var eventId))
        {
            if (!SkipUnreadable)
                throw new InvalidDataException($"{ConsumerName} can't read a message (event type '{eventType}')");
            logger.LogWarning("{Consumer} skipped an unreadable message (event type '{EventType}')", ConsumerName, eventType);
            return;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Delivery is at-least-once, so the same event can arrive twice; the second time does nothing.
        if (await db.ProcessedEvents.AnyAsync(p => p.Consumer == ConsumerName && p.EventId == eventId, stoppingToken))
        {
            logger.LogInformation("{Consumer} skipped {EventType} {EventId}: already processed", ConsumerName, eventType, eventId);
            return;
        }

        var now = time.GetUtcNow();
        var afterSave = await ApplyAsync(db, eventId, eventType, eventJson, now, stoppingToken);
        db.ProcessedEvents.Add(new ProcessedEvent(ConsumerName, eventId, now));
        // One SaveChanges is one transaction: the consumer's rows and the processed id are stored together.
        await db.SaveChangesAsync(stoppingToken);
        // Only after the commit, and only for a new event: if the save fails, the retry would otherwise repeat it,
        // and a redelivered duplicate returns above before reaching here.
        afterSave?.Invoke();
    }
}
