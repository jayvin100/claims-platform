using System.Text;
using System.Text.Json;
using Claims.Api.Infrastructure.Data;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Infrastructure.Kafka;

/// <summary>
/// Reads claim-events for one consumer (Notifications, later Reporting) with the delivery rules both need
/// (ADR-0004, ADR-0005, D16): its own consumer group; one message at a time; event ids it has already handled
/// are skipped; the processed id is saved in the same transaction as the consumer's own rows; the Kafka offset
/// is committed only after that; and a message that fails is retried, never skipped.
/// </summary>
public abstract class ClaimEventConsumer(
    IServiceScopeFactory scopes, KafkaOptions kafka, TimeProvider time, ILogger logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>The consumer group id, and the name its processed event ids are recorded under.</summary>
    protected abstract string ConsumerName { get; }

    /// <summary>
    /// Adds this consumer's rows for one event to <paramref name="db"/> without saving them. The caller saves
    /// them together with the processed event id, so both are stored or neither is.
    /// </summary>
    protected abstract Task ApplyAsync(
        AppDbContext db, string eventType, string eventJson, DateTimeOffset now, CancellationToken stoppingToken);

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
        };
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaOptions.Topic);

        try
        {
            while (true)
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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "{Consumer} couldn't handle a message; retrying in {Delay}s", ConsumerName, RetryDelay.TotalSeconds);
                    if (result is not null)
                        RewindTo(consumer, result);
                    await Task.Delay(RetryDelay, stoppingToken);
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
            logger.LogWarning("{Consumer} couldn't rewind to {Offset} ({Reason})", ConsumerName, result.TopicPartitionOffset, ex.Message);
        }
    }

    private async Task HandleAsync(Message<string, string> message, CancellationToken stoppingToken)
    {
        string eventType = Encoding.UTF8.GetString(message.Headers.GetLastBytes(KafkaOptions.EventTypeHeader));
        using var json = JsonDocument.Parse(message.Value);
        var eventId = json.RootElement.GetProperty("eventId").GetGuid();

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Delivery is at-least-once, so the same event can arrive twice; the second time does nothing.
        if (await db.ProcessedEvents.AnyAsync(p => p.Consumer == ConsumerName && p.EventId == eventId, stoppingToken))
        {
            logger.LogInformation("{Consumer} skipped {EventType} {EventId}: already processed", ConsumerName, eventType, eventId);
            return;
        }

        var now = time.GetUtcNow();
        await ApplyAsync(db, eventType, message.Value, now, stoppingToken);
        db.ProcessedEvents.Add(new ProcessedEvent(ConsumerName, eventId, now));
        // One SaveChanges is one transaction: the consumer's rows and the processed id are stored together.
        await db.SaveChangesAsync(stoppingToken);
    }
}
