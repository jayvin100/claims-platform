using System.Text.Json;
using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;
using Claims.Api.Infrastructure.Outbox;

namespace Claims.Api.Reporting;

/// <summary>
/// Keeps the Reporting read model up to date from claim events: one row per Claim (D17). It only knows what the
/// events tell it, never the Claims tables (ADR-0001). The delivery rules (own group, skip duplicates, commit
/// after saving, retry on failure, dead-letter poison) are in <see cref="ClaimEventConsumer"/>.
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
        // Sent to the dead-letter topic, not recorded as processed (D90). The totals miss this event until a person
        // replays it, but the partition keeps moving. ReportingConsumerTests fails for any event type with no rule.
        if (!ClaimEventTypes.TryGetValue(eventType, out var type))
            throw new PoisonMessageException($"unknown event type {eventType}");

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
