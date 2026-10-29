namespace Claims.Api.Claims;

/// <summary>
/// A Claimant's request to be paid for an incident. Its public methods are the only way to change it:
/// each one checks its rule (D12), records a Claim History entry (D20) and raises one domain event (D32).
/// </summary>
public class Claim
{
    public Guid Id { get; private set; }
    public string ClaimNumber { get; private set; } = null!;
    public string PolicyNumber { get; private set; } = null!;
    public Market Market { get; private set; }
    public string Currency { get; private set; } = null!; // from the Market; stored so rows and events carry it
    public ClaimType ClaimType { get; private set; }
    public DateOnly IncidentDate { get; private set; }
    public string Description { get; private set; } = null!;
    public ClaimStatus Status { get; private set; }
    public Guid ClaimantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ClaimHistoryEntry> _history = [];
    public IReadOnlyList<ClaimHistoryEntry> History => _history;

    // Raised but not yet saved. AppDbContext moves them to the outbox when the Claim is saved.
    private readonly List<ClaimEvent> _events = [];
    public IReadOnlyList<ClaimEvent> Events => _events;
    public void ClearEvents() => _events.Clear();

    private Claim() { } // for EF Core

    /// <param name="sequence">The next value of the shared Claim Number sequence (D19).</param>
    public static Claim Submit(
        Guid claimantId, string policyNumber, Market market, ClaimType claimType,
        DateOnly incidentDate, string description, long sequence, DateTimeOffset now)
    {
        if (incidentDate > Markets.TodayIn(market, now))
            throw new InvalidClaimException("The incident date can't be in the future.");

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = $"CLM-{market}-{sequence:D6}",
            PolicyNumber = policyNumber,
            Market = market,
            Currency = Markets.CurrencyOf(market),
            ClaimType = claimType,
            IncidentDate = incidentDate,
            Description = description,
            Status = ClaimStatus.Submitted,
            ClaimantId = claimantId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        claim._history.Add(new ClaimHistoryEntry(ClaimStatus.Submitted, "Claim submitted", now));
        claim._events.Add(new ClaimSubmitted(
            Guid.NewGuid(), claim.Id, claim.ClaimNumber, market, now, claimantId, claim.Currency));
        return claim;
    }
}
