using System.Globalization;

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
    public Guid? AssignedOfficerId { get; private set; } // empty until an Officer picks the Claim up (D11)
    public decimal? ReserveAmount { get; private set; } // empty until set; always in the Claim's Currency
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
        claim.Record("Claim submitted", new ClaimSubmitted(
            Guid.NewGuid(), claim.Id, claim.ClaimNumber, market, now, claimantId, claim.Currency));
        return claim;
    }

    /// <summary>Any Officer picks up a Submitted Claim and becomes its Assigned Officer (D11).</summary>
    public void PickUp(Guid officerId, DateTimeOffset now)
    {
        if (Status != ClaimStatus.Submitted)
            throw new ClaimConflictException("Only a Submitted Claim can be picked up.");

        Status = ClaimStatus.UnderReview;
        AssignedOfficerId = officerId;
        Record("Claim picked up for review", new ClaimPickedUp(Guid.NewGuid(), Id, ClaimNumber, Market, now, officerId, ClaimantId));
    }

    /// <summary>The Assigned Officer sets or revises the Reserve while the Claim is Under Review.</summary>
    public void SetReserve(Guid officerId, decimal amount, DateTimeOffset now)
    {
        EnsureAssignedTo(officerId);
        if (Status != ClaimStatus.UnderReview)
            throw new ClaimConflictException("The Reserve can only be set while the Claim is Under Review.");
        if (amount <= 0)
            throw new InvalidClaimException("The Reserve must be greater than zero.");

        ReserveAmount = amount;
        // Thousands separators and at least two decimals, but never rounded: the timeline shows exactly what was saved.
        // Invariant culture, so the text doesn't depend on the server's locale.
        var shown = amount.ToString("#,0.00##########", CultureInfo.InvariantCulture);
        Record($"Reserve set to {Currency} {shown}", new ReserveSet(Guid.NewGuid(), Id, ClaimNumber, Market, now, amount, Currency));
    }

    /// <summary>The Assigned Officer accepts the Claim at its current Reserve.</summary>
    public void Approve(Guid officerId, DateTimeOffset now)
    {
        EnsureAssignedTo(officerId);
        if (Status != ClaimStatus.UnderReview)
            throw new ClaimConflictException("Only a Claim Under Review can be approved.");
        if (ReserveAmount is null)
            throw new ClaimConflictException("A Claim needs a Reserve before it can be approved.");

        Status = ClaimStatus.Approved;
        Record("Claim approved", new ClaimApproved(Guid.NewGuid(), Id, ClaimNumber, Market, now, ClaimantId));
    }

    /// <summary>The Assigned Officer declines the Claim. The reason goes on the Claimant's timeline.</summary>
    public void Reject(Guid officerId, string reason, DateTimeOffset now)
    {
        EnsureAssignedTo(officerId);
        if (Status != ClaimStatus.UnderReview)
            throw new ClaimConflictException("Only a Claim Under Review can be rejected.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidClaimException("A rejection needs a reason.");

        Status = ClaimStatus.Rejected;
        Record($"Claim rejected: {reason}", new ClaimRejected(Guid.NewGuid(), Id, ClaimNumber, Market, now, ClaimantId, reason));
    }

    /// <summary>The Assigned Officer records that an Approved Claim's Reserve has been paid. Settled is final.</summary>
    public void Settle(Guid officerId, DateTimeOffset now)
    {
        EnsureAssignedTo(officerId);
        if (Status != ClaimStatus.Approved)
            throw new ClaimConflictException("Only an Approved Claim can be settled.");

        Status = ClaimStatus.Settled;
        Record("Claim settled", new ClaimSettled(Guid.NewGuid(), Id, ClaimNumber, Market, now, ClaimantId));
    }

    // Every action ends here, so each one records exactly one Claim History entry (with the new status)
    // and raises exactly one event, both at the event's time.
    private void Record(string description, ClaimEvent @event)
    {
        UpdatedAt = @event.OccurredAt;
        _history.Add(new ClaimHistoryEntry(Status, description, @event.OccurredAt));
        _events.Add(@event);
    }

    // Checked before the status rules, so another Officer gets 403 whatever state the Claim is in (D11).
    // A Submitted Claim has no Assigned Officer yet, so its status rule answers instead.
    private void EnsureAssignedTo(Guid officerId)
    {
        if (AssignedOfficerId is not null && AssignedOfficerId != officerId)
            throw new NotAssignedOfficerException();
    }
}
