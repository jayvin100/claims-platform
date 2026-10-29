using Claims.Api.Claims;
using Claims.Api.Infrastructure.Kafka;

namespace Claims.Api.Reporting;

/// <summary>
/// Reporting's own copy of one Claim, built only from events (ADR-0003, D17). It holds the Claim's current state,
/// not running totals, so the Workload and Exposure figures are worked out fresh on each request and can't drift.
/// </summary>
public class ReportingClaim
{
    public Guid ClaimId { get; private set; }
    public Market Market { get; private set; }
    public string Currency { get; private set; } = null!;
    public ClaimStatus Status { get; private set; }
    public decimal? ReserveAmount { get; private set; } // empty until a ReserveSet arrives: an Unreserved Claim
    public Guid? AssignedOfficerId { get; private set; }

    private ReportingClaim() { } // for EF Core

    /// <summary>
    /// A row for a Claim seen for the first time. ClaimSubmitted always comes first (ADR-0005) and sets the currency
    /// from its event. The Market's currency (D7) is only a fallback, so the row still has one if another event starts it.
    /// </summary>
    public ReportingClaim(Guid claimId, Market market)
    {
        ClaimId = claimId;
        Market = market;
        Currency = Markets.CurrencyOf(market);
        Status = ClaimStatus.Submitted;
    }

    /// <summary>Moves the row to where the event says the Claim now is. Each event type has one effect.</summary>
    public void Apply(ClaimEvent claimEvent)
    {
        switch (claimEvent)
        {
            case ClaimSubmitted e:
                Status = ClaimStatus.Submitted;
                Currency = e.Currency;
                break;
            case ClaimPickedUp e:
                Status = ClaimStatus.UnderReview;
                AssignedOfficerId = e.AssignedOfficerId;
                break;
            case ReserveSet e:
                ReserveAmount = e.Amount; // a revision replaces the earlier figure
                Currency = e.Currency;
                break;
            case InfoRequested:
                Status = ClaimStatus.InfoRequested;
                break;
            case InfoProvided:
                Status = ClaimStatus.UnderReview;
                break;
            case ClaimApproved:
                Status = ClaimStatus.Approved;
                break;
            case ClaimRejected:
                Status = ClaimStatus.Rejected;
                break;
            case ClaimSettled:
                Status = ClaimStatus.Settled;
                break;
            default:
                // A new event type with no rule here. ReportingConsumerTests fails before that can ship; if it did, the
                // message goes to the dead-letter topic (D90) and the totals miss it until it is replayed.
                throw new PoisonMessageException($"Reporting has no rule for {claimEvent.GetType().Name}");
        }
    }
}
