using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;

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

    // What each event does to the row is in ReportingClaim.Apply, the one list of Reporting's rules.
    protected override async Task<Action?> ApplyAsync(
        AppDbContext db, ClaimEvent claimEvent, DateTimeOffset now, CancellationToken stoppingToken)
    {
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
