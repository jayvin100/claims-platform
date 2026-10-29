using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Reporting;

/// <summary>The Workload and Exposure figures, worked out from the Reporting read model alone (D17).</summary>
public static class ReportingQueries
{
    /// <summary>Every Market, in a fixed order, with zeros where it has no open Claims.</summary>
    public static async Task<List<MarketExposureResponse>> Exposure(AppDbContext db)
    {
        var open = await db.ReportingClaims
            .Where(r => r.Status != ClaimStatus.Settled && r.Status != ClaimStatus.Rejected)
            // Grouped by currency too, so two currencies are never summed together, even by mistake.
            .GroupBy(r => new { r.Market, r.Currency })
            .Select(g => new MarketExposureResponse(
                g.Key.Market, g.Key.Currency, g.Count(), g.Sum(r => r.ReserveAmount ?? 0), g.Count(r => r.ReserveAmount == null)))
            .ToListAsync();

        var quiet = Enum.GetValues<Market>()
            .Where(market => open.All(e => e.Market != market))
            .Select(market => new MarketExposureResponse(market, Markets.CurrencyOf(market), 0, 0m, 0));
        return [.. open.Concat(quiet).OrderBy(e => e.Market)];
    }

    /// <summary>Each Officer who holds or has held a Claim, plus the Submitted Claims nobody has picked up.</summary>
    public static async Task<WorkloadResponse> Workload(AppDbContext db)
    {
        // One small query: how many Claims each Officer has in each status (no Officer = not picked up yet).
        var counts = await db.ReportingClaims
            .GroupBy(r => new { r.AssignedOfficerId, r.Status })
            .Select(g => new { g.Key.AssignedOfficerId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var officers = counts
            .Where(c => c.AssignedOfficerId is not null)
            .GroupBy(c => c.AssignedOfficerId!.Value)
            .OrderBy(officer => officer.Key)
            .Select(officer =>
            {
                int Count(ClaimStatus status) => officer.Where(c => c.Status == status).Sum(c => c.Count);
                return new OfficerWorkloadResponse(officer.Key,
                    Count(ClaimStatus.UnderReview), Count(ClaimStatus.InfoRequested), Count(ClaimStatus.Approved),
                    Count(ClaimStatus.Settled), Count(ClaimStatus.Rejected));
            })
            .ToList();
        int unassigned = counts.Where(c => c.Status == ClaimStatus.Submitted).Sum(c => c.Count);
        return new WorkloadResponse(unassigned, officers);
    }
}
