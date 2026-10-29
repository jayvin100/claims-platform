using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Claims.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>Seam 1: the Assigned Officer asks the Claimant a question and the Claimant answers it, through the HTTP API.</summary>
public class InfoRequestTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record HistoryDto(string Status, string Description);

    private record InfoRequestDto(string Question, string? Answer, DateTimeOffset RequestedAt, DateTimeOffset? AnsweredAt);

    private record ClaimDto(Guid Id, string Status, List<HistoryDto> History, List<InfoRequestDto> InfoRequests);

    [Fact]
    public async Task Officer_asks_the_Claimant_answers_and_the_Claim_returns_to_Under_Review_with_both_steps_recorded()
    {
        var claim = await PickedUpBy(Priya, claimant: Daniel);

        var asked = await ReadClaim(await Send(Priya, claim.Id, "info-requests", """{ "question": "Was anyone hurt?" }"""));

        Assert.Equal("InfoRequested", asked.Status);
        var open = Assert.Single(asked.InfoRequests);
        Assert.Equal(("Was anyone hurt?", null, null), (open.Question, open.Answer, open.AnsweredAt));

        // The Claimant sees the question on their Claim and answers it.
        var seenByClaimant = await factory.ClientAs(Daniel).GetFromJsonAsync<ClaimDto>($"/api/claims/{claim.Id}");
        Assert.Equal("Was anyone hurt?", Assert.Single(seenByClaimant!.InfoRequests).Question);
        var answered = await ReadClaim(await Send(Daniel, claim.Id, "info-response", """{ "answer": "Nobody was hurt." }"""));

        Assert.Equal("UnderReview", answered.Status);
        var closed = Assert.Single(answered.InfoRequests);
        Assert.Equal(("Was anyone hurt?", "Nobody was hurt."), (closed.Question, closed.Answer));
        Assert.True(closed.AnsweredAt >= closed.RequestedAt);

        // The Officer sees the answer, and both steps are on the timeline.
        var seenByOfficer = await factory.ClientAs(Priya).GetFromJsonAsync<ClaimDto>($"/api/claims/{claim.Id}");
        Assert.Equal("Nobody was hurt.", Assert.Single(seenByOfficer!.InfoRequests).Answer);
        Assert.Equal(
            [("Submitted", "Claim submitted"), ("UnderReview", "Claim picked up for review"),
             ("InfoRequested", "Info requested"), ("UnderReview", "Info provided")],
            seenByOfficer.History.Select(h => (h.Status, h.Description)));

        var events = await OutboxFor(claim.Id);
        Assert.Equal(["ClaimSubmitted", "ClaimPickedUp", "InfoRequested", "InfoProvided"], events.Select(e => e.Type));
        Assert.Equal((Daniel.Id, "Was anyone hurt?"),
            (events[2].Json.GetProperty("claimantId").GetGuid(), events[2].Json.GetProperty("question").GetString()));
        Assert.Equal(claim.Id, events[3].Json.GetProperty("claimId").GetGuid());
    }

    [Fact]
    public async Task Asking_while_an_Info_Request_is_open_gets_409()
    {
        var claim = await PickedUpBy(Priya);
        (await Send(Priya, claim.Id, "info-requests", """{ "question": "Was anyone hurt?" }""")).EnsureSuccessStatusCode();

        var response = await Send(Priya, claim.Id, "info-requests", """{ "question": "Where did it happen?" }""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("An Info Request is already open. Wait for the Claimant's answer before asking again.", await Detail(response));
    }

    [Fact]
    public async Task Asking_on_a_Claim_nobody_has_picked_up_gets_409()
    {
        var claim = await SubmitAs(Aisha);

        var response = await Send(Priya, claim.Id, "info-requests", """{ "question": "Was anyone hurt?" }""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Info can only be requested while the Claim is Under Review.", await Detail(response));
    }

    [Fact]
    public async Task An_Officer_asking_on_a_Claim_held_by_another_Officer_gets_403()
    {
        var claim = await PickedUpBy(Priya);

        var response = await Send(Marcus, claim.Id, "info-requests", """{ "question": "Was anyone hurt?" }""");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Only the Assigned Officer can act on this Claim.", await Detail(response));
        Assert.Equal(["ClaimSubmitted", "ClaimPickedUp"], (await OutboxFor(claim.Id)).Select(e => e.Type));
    }

    [Fact]
    public async Task An_Officer_answering_an_Info_Request_gets_403()
    {
        var claim = await AskedBy(Priya);

        var response = await Send(Priya, claim.Id, "info-response", """{ "answer": "Nobody was hurt." }""");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("InfoRequested", (await factory.ClientAs(Priya).GetFromJsonAsync<ClaimDto>($"/api/claims/{claim.Id}"))!.Status);
    }

    [Fact]
    public async Task A_Claimant_asking_a_question_gets_403()
    {
        var claim = await PickedUpBy(Priya);

        var response = await Send(Aisha, claim.Id, "info-requests", """{ "question": "Was anyone hurt?" }""");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Another_Claimant_answering_gets_404()
    {
        var claim = await AskedBy(Priya, claimant: Aisha);

        var response = await Send(Daniel, claim.Id, "info-response", """{ "answer": "Nobody was hurt." }""");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["ClaimSubmitted", "ClaimPickedUp", "InfoRequested"], (await OutboxFor(claim.Id)).Select(e => e.Type));
    }

    [Fact]
    public async Task Answering_with_no_open_Info_Request_gets_409()
    {
        var claim = await PickedUpBy(Priya, claimant: Aisha);

        var response = await Send(Aisha, claim.Id, "info-response", """{ "answer": "Nobody was hurt." }""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("There is no open Info Request to answer.", await Detail(response));
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "question": "" }""")]
    [InlineData("""{ "question": "   " }""")]
    public async Task Asking_without_a_question_gets_400(string body)
    {
        var claim = await PickedUpBy(Priya);

        var response = await Send(Priya, claim.Id, "info-requests", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "answer": "" }""")]
    [InlineData("""{ "answer": "   " }""")]
    public async Task Answering_without_an_answer_gets_400(string body)
    {
        var claim = await AskedBy(Priya, claimant: Aisha);

        var response = await Send(Aisha, claim.Id, "info-response", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // D60 (amended): a malformed body gets the framework's 400 before the Claim is loaded,
    // so the answer is the same for any Claim id and does not reveal that the Claim exists.
    [Fact]
    public async Task A_malformed_body_gets_400_before_ownership_is_checked()
    {
        var claim = await AskedBy(Priya, claimant: Aisha);

        var response = await Send(Daniel, claim.Id, "info-response", """{ "answer": "" }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["ClaimSubmitted", "ClaimPickedUp", "InfoRequested"], (await OutboxFor(claim.Id)).Select(e => e.Type));
    }

    private async Task<ClaimDto> PickedUpBy(DemoUser officer, DemoUser? claimant = null)
    {
        var claim = await SubmitAs(claimant ?? Aisha);
        (await factory.ClientAs(officer).PostAsync($"/api/claims/{claim.Id}/pick-up", null)).EnsureSuccessStatusCode();
        return claim;
    }

    private async Task<ClaimDto> AskedBy(DemoUser officer, DemoUser? claimant = null)
    {
        var claim = await PickedUpBy(officer, claimant);
        (await Send(officer, claim.Id, "info-requests", """{ "question": "Was anyone hurt?" }""")).EnsureSuccessStatusCode();
        return claim;
    }

    private Task<HttpResponseMessage> Send(DemoUser user, Guid claimId, string action, string body) =>
        factory.ClientAs(user).PostAsync($"/api/claims/{claimId}/{action}", new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    private async Task<List<(string Type, JsonElement Json)>> OutboxFor(Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.OutboxMessages.Where(m => m.ClaimId == claimId).OrderBy(m => m.Sequence).ToListAsync();
        // Clone so each payload outlives its JsonDocument.
        return rows.Select(r => (r.Type, JsonDocument.Parse(r.Payload).RootElement.Clone())).ToList();
    }

    private static async Task<ClaimDto> ReadClaim(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    private async Task<ClaimDto> SubmitAs(DemoUser claimant)
    {
        var claim = new
        {
            policyNumber = "POL-123", market = "SG", claimType = "Motor",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            description = "Rear-ended at a junction",
        };
        var response = await factory.ClientAs(claimant).PostAsJsonAsync("/api/claims", claim);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }
}
