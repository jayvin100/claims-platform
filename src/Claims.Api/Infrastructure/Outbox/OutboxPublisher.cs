using System.Text;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Infrastructure.Outbox;

/// <summary>
/// The single publisher of outbox rows to Kafka (ADR-0004, ADR-0005). Every second it sends unsent rows one at a
/// time in sequence order and marks each sent only after Kafka acknowledges it. If a send fails it stops, and the
/// next poll starts again from that same row, so a Claim's later events can never overtake an earlier one.
/// </summary>
public class OutboxPublisher(
    IServiceScopeFactory scopes, KafkaOptions kafka, TimeProvider time, ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ProducerConfig(kafka.ClientConfig())
        {
            // Acknowledged only once the broker has stored the message, so "sent" really means stored.
            Acks = Acks.All,
            // Give up on a send after 10 s (the default is 5 minutes), so while Kafka is down the worker
            // logs a warning and retries instead of waiting silently.
            MessageTimeoutMs = 10_000,
        };
        using var producer = new ProducerBuilder<string, string>(config).Build();
        using var timer = new PeriodicTimer(PollInterval, time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishPendingAsync(producer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // The host is stopping.
            }
            catch (Exception ex)
            {
                // Any other failure, including an OperationCanceledException from a timeout, is retried:
                // the failed row is still unsent and is the first one the next poll picks up.
                logger.LogWarning(ex, "Couldn't publish outbox events; retrying");
            }
        }
    }

    private async Task PublishPendingAsync(IProducer<string, string> producer, CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // "Not yet sent" rather than "after the last one sent": a row whose transaction committed late,
        // behind a higher sequence number, is still picked up. So the order kept is per Claim, not global:
        // events of different Claims can be published out of sequence order. Within one Claim it holds,
        // because each save raises one event and is checked against the Claim's xmin (D13), so a Claim's
        // events commit one after another, in sequence order.
        var pending = await db.OutboxMessages
            .Where(m => m.SentAt == null)
            .OrderBy(m => m.Sequence)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        foreach (var row in pending)
        {
            // Awaiting each send before starting the next keeps one message in flight, so order holds.
            // The Claim id key puts all of a Claim's events on one partition, where Kafka keeps their order.
            await producer.ProduceAsync(KafkaOptions.Topic, new Message<string, string>
            {
                Key = row.ClaimId.ToString(),
                Value = row.Payload,
                Headers = new Headers { { KafkaOptions.EventTypeHeader, Encoding.UTF8.GetBytes(row.Type) } },
            }, stoppingToken);

            // If the process dies between the send and this save, the row is sent again on restart.
            // That's the at-least-once half; consumers skip event ids they've already seen (D16).
            row.MarkSent(time.GetUtcNow());
            await db.SaveChangesAsync(stoppingToken);
            logger.LogInformation("Published {EventType} {EventId} for Claim {ClaimId}", row.Type, row.EventId, row.ClaimId);
        }
    }
}
