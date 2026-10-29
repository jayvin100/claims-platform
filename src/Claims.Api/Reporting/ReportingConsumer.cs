using System.Text.Json;
using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;
using Claims.Api.Infrastructure.Outbox;

namespace Claims.Api.Reporting;

/// <summary>
/// Keeps the Reporting read model up to date from claim events: one row per Claim (D17). It only knows what the
/// events tell it, never the Claims tables (ADR-0001). The delivery rules (own group, skip duplicates, commit
/// after saving, retry on failure) are in <see cref="ClaimEventConsumer"/>.
/// </summary>
public class ReportingConsumer(
    IServiceScopeFactory scopes, KafkaOptions kafka, TimeProvider time, ILogger<ReportingConsumer> logger)
    : ClaimEventConsumer(scopes, kafka, time, logger)
{
    /// <summary>The consumer group, and the name its processed event ids are recorded under.</summary>
    public const string Name = "reporting";

    protected override string ConsumerName => Name;

    /// <summary>The event types Reporting reads, by the name in the Kafka header. All eight change the row (D32).</summary>
    public static readonly IReadOnlyDictionary<string, Type> EventTypes = new[]
    {
        typeof(ClaimSubmitted), typeof(ClaimPickedUp), typeof(ReserveSet), typeof(InfoRequested),
        typeof(InfoProvided), typeof(ClaimApproved), typeof(ClaimRejected), typeof(ClaimSettled),
    }.ToDictionary(t => t.Name);

    protected override async Task<Action?> ApplyAsync(
        AppDbContext db, Guid eventId, string eventType, string eventJson, DateTimeOffset now, CancellationToken stoppingToken)
    {
        if (!EventTypes.TryGetValue(eventType, out var type))
        {
            // Skipped (recorded as processed), not retried, as in Notifications (D68): a retry would block every
            // Claim on its partition. ReportingConsumerTests fails for any event type missing from the list.
            logger.LogWarning("{Consumer} has no rule for unknown event type {EventType} {EventId}; skipped",
                ConsumerName, eventType, eventId);
            return null;
        }

        var claimEvent = (ClaimEvent)JsonSerializer.Deserialize(eventJson, type, OutboxMessage.JsonOptions)!;
        var row = await db.ReportingClaims.FindAsync([claimEvent.ClaimId], stoppingToken);
        if (row is null)
        {
            row = new ReportingClaim(claimEvent.ClaimId, claimEvent.Market);
            db.ReportingClaims.Add(row);
        }
        row.Apply(claimEvent);
        return null; // nothing to do after the save
    }
}
