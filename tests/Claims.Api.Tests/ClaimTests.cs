using System.Globalization;
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

    private static readonly Guid OfficerId = Guid.NewGuid(); // no database here, so any id will do
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
        var historyCount = claim.History.Count;

        var error = Assert.Throws<ClaimConflictException>(() => claim.PickUp(Guid.NewGuid(), Later));

        Assert.Equal("Only a Submitted Claim can be picked up.", error.Message);
        Assert.Equal(status, claim.Status);
        Assert.Equal(OfficerId, claim.AssignedOfficerId);
        Assert.Equal(historyCount, claim.History.Count);
        Assert.Empty(claim.Events);
    }

    [Fact]
    public void Assigned_Officer_sets_a_Reserve_on_a_Claim_Under_Review_in_the_Markets_currency()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        claim.SetReserve(OfficerId, 1500.50m, Later);

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(1500.50m, claim.ReserveAmount);
        Assert.Equal(Later, claim.UpdatedAt);
        var entry = claim.History[^1];
        Assert.Equal((ClaimStatus.UnderReview, "Reserve set to SGD 1,500.50", Later), (entry.Status, entry.Description, entry.OccurredAt));
        var reserveSet = Assert.IsType<ReserveSet>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, reserveSet.EventId);
        Assert.Equal((claim.Id, claim.ClaimNumber, Market.SG, Later), (reserveSet.ClaimId, reserveSet.ClaimNumber, reserveSet.Market, reserveSet.OccurredAt));
        Assert.Equal((1500.50m, "SGD"), (reserveSet.Amount, reserveSet.Currency));
    }

    [Fact]
    public void Assigned_Officer_revises_the_Reserve_while_the_Claim_is_Under_Review()
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        claim.SetReserve(OfficerId, 1000m, Later);

        claim.SetReserve(OfficerId, 2500m, Later.AddMinutes(5));

        Assert.Equal(2500m, claim.ReserveAmount);
        Assert.Equal("Reserve set to SGD 2,500.00", claim.History[^1].Description);
        Assert.Equal([1000m, 2500m], claim.Events.Cast<ReserveSet>().Select(e => e.Amount));
    }

    [Fact]
    public void The_Claim_History_shows_the_Reserve_exactly_as_set_never_rounded()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        claim.SetReserve(OfficerId, 1000.005m, Later);

        Assert.Equal("Reserve set to SGD 1,000.005", claim.History[^1].Description);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("-500")]
    public void A_Reserve_of_zero_or_less_is_refused(string amount)
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        var historyCount = claim.History.Count;

        var error = Assert.Throws<InvalidClaimException>(() => claim.SetReserve(OfficerId, decimal.Parse(amount, CultureInfo.InvariantCulture), Later));

        Assert.Equal("The Reserve must be greater than zero.", error.Message);
        Assert.Null(claim.ReserveAmount);
        Assert.Equal(historyCount, claim.History.Count);
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.InfoRequested)]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Settled)]
    [InlineData(ClaimStatus.Rejected)]
    public void Setting_a_Reserve_is_refused_from_every_status_except_Under_Review(ClaimStatus status)
    {
        var claim = InStatus(status);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.SetReserve(OfficerId, 1000m, Later));

        Assert.Equal("The Reserve can only be set while the Claim is Under Review.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Fact]
    public void Assigned_Officer_approves_a_Claim_Under_Review_at_its_Reserve()
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        claim.SetReserve(OfficerId, 1500m, Later);
        claim.ClearEvents();

        claim.Approve(OfficerId, Later.AddMinutes(5));

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(1500m, claim.ReserveAmount);
        Assert.Equal(Later.AddMinutes(5), claim.UpdatedAt);
        var entry = claim.History[^1];
        Assert.Equal((ClaimStatus.Approved, "Claim approved", Later.AddMinutes(5)), (entry.Status, entry.Description, entry.OccurredAt));
        var approved = Assert.IsType<ClaimApproved>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, approved.EventId);
        Assert.Equal((claim.Id, claim.ClaimNumber, Market.SG, Later.AddMinutes(5)), (approved.ClaimId, approved.ClaimNumber, approved.Market, approved.OccurredAt));
        Assert.Equal(ClaimantId, approved.ClaimantId);
    }

    [Fact]
    public void Approving_a_Claim_with_no_Reserve_is_refused()
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.Approve(OfficerId, Later));

        Assert.Equal("A Claim needs a Reserve before it can be approved.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.InfoRequested)]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Settled)]
    [InlineData(ClaimStatus.Rejected)]
    public void Approving_is_refused_from_every_status_except_Under_Review(ClaimStatus status)
    {
        var claim = InStatus(status);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.Approve(OfficerId, Later));

        Assert.Equal("Only a Claim Under Review can be approved.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Fact]
    public void Assigned_Officer_rejects_a_Claim_Under_Review_with_a_reason_the_Claimant_can_see()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        claim.Reject(OfficerId, "Policy had lapsed", Later);

        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Equal(Later, claim.UpdatedAt);
        var entry = claim.History[^1];
        Assert.Equal((ClaimStatus.Rejected, "Claim rejected: Policy had lapsed", Later), (entry.Status, entry.Description, entry.OccurredAt));
        var rejected = Assert.IsType<ClaimRejected>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, rejected.EventId);
        Assert.Equal((claim.Id, claim.ClaimNumber, Market.SG, Later), (rejected.ClaimId, rejected.ClaimNumber, rejected.Market, rejected.OccurredAt));
        Assert.Equal((ClaimantId, "Policy had lapsed"), (rejected.ClaimantId, rejected.Reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejecting_without_a_reason_is_refused(string reason)
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        var before = Snapshot(claim);

        var error = Assert.Throws<InvalidClaimException>(() => claim.Reject(OfficerId, reason, Later));

        Assert.Equal("A rejection needs a reason.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.InfoRequested)]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Settled)]
    [InlineData(ClaimStatus.Rejected)]
    public void Rejecting_is_refused_from_every_status_except_Under_Review(ClaimStatus status)
    {
        var claim = InStatus(status);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.Reject(OfficerId, "Policy had lapsed", Later));

        Assert.Equal("Only a Claim Under Review can be rejected.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Fact]
    public void Assigned_Officer_settles_an_Approved_Claim()
    {
        var claim = InStatus(ClaimStatus.Approved);

        claim.Settle(OfficerId, Later.AddDays(1));

        Assert.Equal(ClaimStatus.Settled, claim.Status);
        Assert.Equal(Later.AddDays(1), claim.UpdatedAt);
        var entry = claim.History[^1];
        Assert.Equal((ClaimStatus.Settled, "Claim settled", Later.AddDays(1)), (entry.Status, entry.Description, entry.OccurredAt));
        var settled = Assert.IsType<ClaimSettled>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, settled.EventId);
        Assert.Equal((claim.Id, claim.ClaimNumber, Market.SG, Later.AddDays(1)), (settled.ClaimId, settled.ClaimNumber, settled.Market, settled.OccurredAt));
        Assert.Equal(ClaimantId, settled.ClaimantId);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.InfoRequested)]
    [InlineData(ClaimStatus.Settled)]
    [InlineData(ClaimStatus.Rejected)]
    public void Settling_is_refused_from_every_status_except_Approved(ClaimStatus status)
    {
        var claim = InStatus(status);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.Settle(OfficerId, Later));

        Assert.Equal("Only an Approved Claim can be settled.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData("set Reserve", ClaimStatus.UnderReview)]
    [InlineData("approve", ClaimStatus.UnderReview)]
    [InlineData("reject", ClaimStatus.UnderReview)]
    [InlineData("settle", ClaimStatus.Approved)]
    [InlineData("request info", ClaimStatus.UnderReview)]
    public void An_Officer_who_is_not_the_Assigned_Officer_is_refused_every_action(string action, ClaimStatus status)
    {
        var claim = InStatus(status);
        if (status == ClaimStatus.UnderReview)
            claim.SetReserve(OfficerId, 1500m, Later); // so only the Officer rule can stop approve
        claim.ClearEvents();
        var before = Snapshot(claim);
        var otherOfficerId = Guid.NewGuid();

        Action act = action switch
        {
            "set Reserve" => () => claim.SetReserve(otherOfficerId, 1000m, Later),
            "approve" => () => claim.Approve(otherOfficerId, Later),
            "reject" => () => claim.Reject(otherOfficerId, "Policy had lapsed", Later),
            "request info" => () => claim.RequestInfo(otherOfficerId, "Was anyone hurt?", Later),
            _ => () => claim.Settle(otherOfficerId, Later),
        };
        var error = Assert.Throws<NotAssignedOfficerException>(act);

        Assert.Equal("Only the Assigned Officer can act on this Claim.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Fact]
    public void Assigned_Officer_asks_the_Claimant_a_question_which_moves_the_Claim_to_Info_Requested()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        claim.RequestInfo(OfficerId, "Was anyone hurt?", Later);

        Assert.Equal(ClaimStatus.InfoRequested, claim.Status);
        Assert.Equal(Later, claim.UpdatedAt);
        var infoRequest = Assert.Single(claim.InfoRequests);
        Assert.Equal(("Was anyone hurt?", Later), (infoRequest.Question, infoRequest.RequestedAt));
        Assert.Null(infoRequest.Answer);
        Assert.Null(infoRequest.AnsweredAt);
        var entry = claim.History[^1];
        Assert.Equal((ClaimStatus.InfoRequested, "Info requested", Later), (entry.Status, entry.Description, entry.OccurredAt));
        var requested = Assert.IsType<InfoRequested>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, requested.EventId);
        Assert.Equal((claim.Id, claim.ClaimNumber, Market.SG, Later), (requested.ClaimId, requested.ClaimNumber, requested.Market, requested.OccurredAt));
        Assert.Equal((ClaimantId, "Was anyone hurt?"), (requested.ClaimantId, requested.Question));
    }

    [Fact]
    public void Asking_while_an_Info_Request_is_open_is_refused()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.RequestInfo(OfficerId, "And the other driver's details?", Later));

        Assert.Equal("An Info Request is already open. Wait for the Claimant's answer before asking again.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Settled)]
    [InlineData(ClaimStatus.Rejected)]
    public void Asking_is_refused_from_every_status_except_Under_Review(ClaimStatus status)
    {
        var claim = InStatus(status);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.RequestInfo(OfficerId, "Was anyone hurt?", Later));

        Assert.Equal("Info can only be requested while the Claim is Under Review.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Asking_without_a_question_is_refused(string question)
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        var before = Snapshot(claim);

        var error = Assert.Throws<InvalidClaimException>(() => claim.RequestInfo(OfficerId, question, Later));

        Assert.Equal("An Info Request needs a question.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Fact]
    public void Claimant_answers_the_open_Info_Request_which_returns_the_Claim_to_Under_Review()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);
        var answeredAt = Later.AddHours(2);

        claim.ProvideInfo("Nobody was hurt.", answeredAt);

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(answeredAt, claim.UpdatedAt);
        var infoRequest = Assert.Single(claim.InfoRequests);
        Assert.Equal(("Was anyone hurt?", "Nobody was hurt.", answeredAt), (infoRequest.Question, infoRequest.Answer, infoRequest.AnsweredAt));
        Assert.False(infoRequest.IsOpen);
        var entry = claim.History[^1];
        Assert.Equal((ClaimStatus.UnderReview, "Info provided", answeredAt), (entry.Status, entry.Description, entry.OccurredAt));
        var provided = Assert.IsType<InfoProvided>(Assert.Single(claim.Events));
        Assert.NotEqual(Guid.Empty, provided.EventId);
        Assert.Equal((claim.Id, claim.ClaimNumber, Market.SG, answeredAt), (provided.ClaimId, provided.ClaimNumber, provided.Market, provided.OccurredAt));
    }

    [Fact]
    public void Asking_again_after_an_answer_keeps_the_earlier_question_and_answer()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);
        claim.ProvideInfo("Nobody was hurt.", Later.AddHours(1));

        claim.RequestInfo(OfficerId, "Can you send the police report number?", Later.AddHours(2));

        Assert.Equal(ClaimStatus.InfoRequested, claim.Status);
        Assert.Equal(
            [("Was anyone hurt?", "Nobody was hurt."), ("Can you send the police report number?", null)],
            claim.InfoRequests.Select(r => (r.Question, r.Answer)));
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Settled)]
    [InlineData(ClaimStatus.Rejected)]
    public void Answering_with_no_open_Info_Request_is_refused(ClaimStatus status)
    {
        var claim = InStatus(status);
        var before = Snapshot(claim);

        var error = Assert.Throws<ClaimConflictException>(() => claim.ProvideInfo("Nobody was hurt.", Later));

        Assert.Equal("There is no open Info Request to answer.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    [Fact]
    public void An_answered_Info_Request_cannot_be_answered_again()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);
        claim.ProvideInfo("Nobody was hurt.", Later);
        claim.ClearEvents();

        var error = Assert.Throws<ClaimConflictException>(() => claim.ProvideInfo("Actually, one passenger was.", Later));

        Assert.Equal("There is no open Info Request to answer.", error.Message);
        Assert.Equal("Nobody was hurt.", Assert.Single(claim.InfoRequests).Answer);
        Assert.Empty(claim.Events);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Answering_without_an_answer_is_refused(string answer)
    {
        var claim = InStatus(ClaimStatus.InfoRequested);
        var before = Snapshot(claim);

        var error = Assert.Throws<InvalidClaimException>(() => claim.ProvideInfo(answer, Later));

        Assert.Equal("An answer can't be empty.", error.Message);
        Assert.Equal(before, Snapshot(claim));
        Assert.Empty(claim.Events);
    }

    // Reaches a status through the Claim's own methods, with OfficerId as the Assigned Officer and no
    // events left over. Info Requested leaves one open Info Request.
    private static Claim InStatus(ClaimStatus status)
    {
        var claim = Submit(Market.SG, new DateOnly(2026, 10, 28));
        if (status != ClaimStatus.Submitted)
            claim.PickUp(OfficerId, Later);

        switch (status)
        {
            case ClaimStatus.InfoRequested:
                claim.RequestInfo(OfficerId, "Was anyone hurt?", Later);
                break;
            case ClaimStatus.Approved or ClaimStatus.Settled:
                claim.SetReserve(OfficerId, 1500m, Later);
                claim.Approve(OfficerId, Later);
                if (status == ClaimStatus.Settled)
                    claim.Settle(OfficerId, Later);
                break;
            case ClaimStatus.Rejected:
                claim.Reject(OfficerId, "Policy had lapsed", Later);
                break;
        }
        claim.ClearEvents();
        return claim;
    }

    // What a refused action must leave as it was. It must also raise no event.
    private static (ClaimStatus, Guid?, decimal?, int, int, int) Snapshot(Claim claim) =>
        (claim.Status, claim.AssignedOfficerId, claim.ReserveAmount, claim.History.Count,
         claim.InfoRequests.Count, claim.InfoRequests.Count(r => r.IsOpen));

    private static Claim Submit(Market market, DateOnly incidentDate, long sequence = 1) =>
        Claim.Submit(ClaimantId, "POL-1", market, ClaimType.Motor, incidentDate, "Rear-ended at a junction", sequence, Now);
}
