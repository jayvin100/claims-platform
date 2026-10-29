using Claims.Api.Claims;

namespace Claims.Api.Tests;

/// <summary>Seam 2: the Claim's public methods, with no database.</summary>
public class ClaimTests
{
    private static readonly Guid ClaimantId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    // 10:00 in Singapore on 29 Oct 2026.
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Submit_creates_a_Submitted_Claim_with_its_Claim_Number_and_Market_currency()
    {
        var claim = Submit(Market.SG, new DateOnly(2026, 10, 28), sequence: 123);

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal("CLM-SG-000123", claim.ClaimNumber);
        Assert.Equal("SGD", claim.Currency);
        Assert.Equal(ClaimantId, claim.ClaimantId);
        Assert.Equal("POL-1", claim.PolicyNumber);
        Assert.Equal(ClaimType.Motor, claim.ClaimType);
        Assert.Equal(new DateOnly(2026, 10, 28), claim.IncidentDate);
        Assert.Equal("Rear-ended at a junction", claim.Description);
        Assert.Equal(Now, claim.CreatedAt);
        Assert.Equal(Now, claim.UpdatedAt);
    }

    [Theory]
    [InlineData(Market.SG, "SGD")]
    [InlineData(Market.MY, "MYR")]
    [InlineData(Market.HK, "HKD")]
    [InlineData(Market.TH, "THB")]
    [InlineData(Market.ID, "IDR")]
    [InlineData(Market.PH, "PHP")]
    public void Submit_sets_the_currency_from_the_Market(Market market, string currency)
    {
        Assert.Equal(currency, Submit(market, new DateOnly(2026, 10, 28)).Currency);
    }

    [Fact]
    public void Submit_records_a_Submitted_entry_in_the_Claim_History()
    {
        var claim = Submit(Market.SG, new DateOnly(2026, 10, 28));

        var entry = Assert.Single(claim.History);
        Assert.Equal(ClaimStatus.Submitted, entry.Status);
        Assert.Equal("Claim submitted", entry.Description);
        Assert.Equal(Now, entry.OccurredAt);
    }

    [Fact]
    public void Submit_raises_ClaimSubmitted_with_the_Claimant_and_currency()
    {
        var claim = Submit(Market.MY, new DateOnly(2026, 10, 28), sequence: 7);

        var submitted = Assert.IsType<ClaimSubmitted>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, submitted.EventId);
        Assert.Equal(claim.Id, submitted.ClaimId);
        Assert.Equal("CLM-MY-000007", submitted.ClaimNumber);
        Assert.Equal(Market.MY, submitted.Market);
        Assert.Equal(Now, submitted.OccurredAt);
        Assert.Equal(ClaimantId, submitted.ClaimantId);
        Assert.Equal("MYR", submitted.Currency);
    }

    [Fact]
    public void Submit_refuses_an_incident_date_in_the_future()
    {
        var error = Assert.Throws<InvalidClaimException>(() => Submit(Market.SG, new DateOnly(2026, 10, 30)));

        Assert.Equal("The incident date can't be in the future.", error.Message);
    }

    [Fact]
    public void Submit_accepts_an_incident_that_happened_today()
    {
        var claim = Submit(Market.SG, new DateOnly(2026, 10, 29));

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
    }

    [Fact]
    public void Submit_judges_today_by_the_Markets_own_clock()
    {
        // 01:00 on 30 Oct in Singapore is still 29 Oct in UTC.
        var justAfterMidnightInSingapore = new DateTimeOffset(2026, 10, 29, 17, 0, 0, TimeSpan.Zero);

        var claim = Claim.Submit(ClaimantId, "POL-1", Market.SG, ClaimType.Motor,
            new DateOnly(2026, 10, 30), "Rear-ended at a junction", 1, justAfterMidnightInSingapore);

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
    }

    private static readonly Guid OfficerId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Later = Now.AddHours(1);

    [Fact]
    public void Pick_up_makes_the_Officer_the_Assigned_Officer_and_moves_a_Submitted_Claim_to_Under_Review()
    {
        var claim = Submit(Market.SG, new DateOnly(2026, 10, 28));
        claim.ClearEvents();

        claim.PickUp(OfficerId, Later);

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(OfficerId, claim.AssignedOfficerId);
        Assert.Equal(Later, claim.UpdatedAt);
        var entry = claim.History[^1];
        Assert.Equal((ClaimStatus.UnderReview, "Claim picked up for review", Later), (entry.Status, entry.Description, entry.OccurredAt));
        var pickedUp = Assert.IsType<ClaimPickedUp>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, pickedUp.EventId);
        Assert.Equal((claim.Id, claim.ClaimNumber, Market.SG, Later), (pickedUp.ClaimId, pickedUp.ClaimNumber, pickedUp.Market, pickedUp.OccurredAt));
        Assert.Equal(OfficerId, pickedUp.AssignedOfficerId);
        Assert.Equal(ClaimantId, pickedUp.ClaimantId);
    }

    [Fact]
    public void Pick_up_of_a_Claim_already_picked_up_is_refused_and_the_Assigned_Officer_is_unchanged()
    {
        var claim = Submit(Market.SG, new DateOnly(2026, 10, 28));
        claim.PickUp(OfficerId, Later);
        claim.ClearEvents();

        var error = Assert.Throws<ClaimConflictException>(() => claim.PickUp(Guid.NewGuid(), Later.AddMinutes(1)));

        Assert.Equal("Only a Submitted Claim can be picked up.", error.Message);
        Assert.Equal(OfficerId, claim.AssignedOfficerId);
        Assert.Equal(2, claim.History.Count);
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData(ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.InfoRequested)]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Settled)]
    [InlineData(ClaimStatus.Rejected)]
    public void Pick_up_is_refused_from_every_status_except_Submitted(ClaimStatus status)
    {
        var claim = InStatus(status);

        var error = Assert.Throws<ClaimConflictException>(() => claim.PickUp(OfficerId, Later));

        Assert.Equal("Only a Submitted Claim can be picked up.", error.Message);
        Assert.Equal(status, claim.Status);
    }

    // The Claim's methods to reach some statuses (approve, settle...) come in later tickets, so this sets
    // the status the way EF Core does when it loads a row: through the private setter.
    private static Claim InStatus(ClaimStatus status)
    {
        var claim = Submit(Market.SG, new DateOnly(2026, 10, 28));
        typeof(Claim).GetProperty(nameof(Claim.Status))!.SetValue(claim, status);
        return claim;
    }

    private static Claim Submit(Market market, DateOnly incidentDate, long sequence = 1) =>
        Claim.Submit(ClaimantId, "POL-1", market, ClaimType.Motor, incidentDate, "Rear-ended at a junction", sequence, Now);
}
