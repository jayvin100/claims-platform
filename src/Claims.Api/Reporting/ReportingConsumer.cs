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

    /// <summary>
    /// Every claim event type, by the name in the Kafka header (the class name, see OutboxMessage.From). Only used to
    /// read the JSON into the right type: what each event does to the row is in <see cref="ReportingClaim.Apply"/>,
    /// the one list of Reporting's rules.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Type> ClaimEventTypes = typeof(ClaimEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(ClaimEvent)))
        .ToDictionary(t => t.Name);

    protected override async Task<Action?> ApplyAsync(
        AppDbContext db, Guid eventId, string eventType, string eventJson, DateTimeOffset now, CancellationToken stoppingToken)
    {
        if (!ClaimEventTypes.TryGetValue(eventType, out var type))
        {
            // Thrown, so the message is retried: it is not recorded as processed and its offset is not committed, so
            // its partition stays blocked until a fix is deployed. For a read model, correct totals matter more than
            // availability. This is a scoped exception to D68, which still applies to Notifications.
            throw new InvalidOperationException($"{ConsumerName} has no rule for unknown event type {eventType} {eventId}");
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
