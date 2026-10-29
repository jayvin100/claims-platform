using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Claims.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>Seam 1: the Assigned Officer sets a Reserve, approves or rejects, and settles, through the HTTP API.</summary>
public class AssessAndDecideTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record HistoryDto(string Status, string Description);

    private record ClaimDto(Guid Id, string ClaimNumber, string Status, string Currency, decimal? ReserveAmount, List<HistoryDto> History);

    [Fact]
    public async Task Claim_goes_from_submit_to_settle_and_each_step_is_on_the_timeline_and_in_the_outbox()
    {
        var claim = await SubmitAs(Aisha, market: "MY");
        (await factory.ClientAs(Priya).PostAsync($"/api/claims/{claim.Id}/pick-up", null)).EnsureSuccessStatusCode();
        var priya = factory.ClientAs(Priya);

        var reserved = await ReadClaim(await priya.PutAsJsonAsync($"/api/claims/{claim.Id}/reserve", new { amount = 1000m }));
        var revised = await ReadClaim(await priya.PutAsJsonAsync($"/api/claims/{claim.Id}/reserve", new { amount = 1250.75m }));
        var approved = await ReadClaim(await priya.PostAsync($"/api/claims/{claim.Id}/approve", null));
        var settled = await ReadClaim(await priya.PostAsync($"/api/claims/{claim.Id}/settle", null));

        Assert.Equal(("UnderReview", 1000m), (reserved.Status, reserved.ReserveAmount));
        Assert.Equal(("UnderReview", 1250.75m), (revised.Status, revised.ReserveAmount));
        Assert.Equal(("Approved", 1250.75m), (approved.Status, approved.ReserveAmount));
        Assert.Equal(("Settled", 1250.75m, "MYR"), (settled.Status, settled.ReserveAmount, settled.Currency));
        Assert.Equal(
            ["Claim submitted", "Claim picked up for review", "Reserve set to MYR 1,000.00", "Reserve set to MYR 1,250.75", "Claim approved", "Claim settled"],
            settled.History.Select(h => h.Description));

        // The Claimant sees the same Settled Claim.
        var seenByClaimant = await factory.ClientAs(Aisha).GetFromJsonAsync<ClaimDto>($"/api/claims/{claim.Id}");
        Assert.Equal("Settled", seenByClaimant!.Status);

        var events = await OutboxFor(claim.Id);
        Assert.Equal(["ClaimSubmitted", "ClaimPickedUp", "ReserveSet", "ReserveSet", "ClaimApproved", "ClaimSettled"], events.Select(e => e.Type));
        Assert.Equal((1250.75m, "MYR"), (events[3].Json.GetProperty("amount").GetDecimal(), events[3].Json.GetProperty("currency").GetString()));
        Assert.Equal(Aisha.Id, events[4].Json.GetProperty("claimantId").GetGuid());
        Assert.Equal(Aisha.Id, events[5].Json.GetProperty("claimantId").GetGuid());
    }

    [Fact]
    public async Task Assigned_Officer_rejects_with_a_reason_the_Claimant_sees_and_ClaimRejected_carries_it()
    {
        var claim = await PickedUpBy(Priya, claimant: Daniel);

        var rejected = await ReadClaim(await factory.ClientAs(Priya)
            .PostAsJsonAsync($"/api/claims/{claim.Id}/reject", new { reason = "Policy had lapsed" }));

        Assert.Equal("Rejected", rejected.Status);
        var seenByClaimant = await factory.ClientAs(Daniel).GetFromJsonAsync<ClaimDto>($"/api/claims/{claim.Id}");
        Assert.Equal(("Rejected", "Claim rejected: Policy had lapsed"), (seenByClaimant!.History[^1].Status, seenByClaimant.History[^1].Description));
        var (type, json) = (await OutboxFor(claim.Id))[^1];
        Assert.Equal("ClaimRejected", type);
        Assert.Equal((Daniel.Id, "Policy had lapsed"), (json.GetProperty("claimantId").GetGuid(), json.GetProperty("reason").GetString()));
    }

    [Theory]
    [InlineData("PUT", "reserve", """{ "amount": 500 }""")]
    [InlineData("POST", "approve", null)]
    [InlineData("POST", "reject", """{ "reason": "Not covered" }""")]
    [InlineData("POST", "settle", null)]
    public async Task An_Officer_acting_on_a_Claim_held_by_another_Officer_gets_403(string method, string action, string? body)
    {
        var claim = await PickedUpBy(Priya);

        var response = await Send(Marcus, method, claim.Id, action, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Only the Assigned Officer can act on this Claim.", await Detail(response));
        Assert.Equal(["ClaimSubmitted", "ClaimPickedUp"], (await OutboxFor(claim.Id)).Select(e => e.Type));
    }

    [Theory]
    [InlineData("PUT", "reserve", """{ "amount": 500 }""")]
    [InlineData("POST", "approve", null)]
    [InlineData("POST", "reject", """{ "reason": "Not covered" }""")]
    [InlineData("POST", "settle", null)]
    public async Task A_Claimant_calling_an_Officer_action_gets_403(string method, string action, string? body)
    {
        var claim = await PickedUpBy(Priya);

        var response = await Send(Aisha, method, claim.Id, action, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approving_without_a_Reserve_gets_409_naming_the_rule()
    {
        var claim = await PickedUpBy(Priya);

        var response = await factory.ClientAs(Priya).PostAsync($"/api/claims/{claim.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("A Claim needs a Reserve before it can be approved.", await Detail(response));
    }

    [Theory]
    [InlineData("PUT", "reserve", """{ "amount": 500 }""", "The Reserve can only be set while the Claim is Under Review.")]
    [InlineData("POST", "approve", null, "Only a Claim Under Review can be approved.")]
    [InlineData("POST", "reject", """{ "reason": "Not covered" }""", "Only a Claim Under Review can be rejected.")]
    [InlineData("POST", "settle", null, "Only an Approved Claim can be settled.")]
    public async Task A_Settled_Claim_allows_nothing_further_and_gets_409(string method, string action, string? body, string rule)
    {
        var claim = await SettledBy(Priya);

        var response = await Send(Priya, method, claim.Id, action, body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(rule, await Detail(response));
    }

    [Fact]
    public async Task Settling_a_Claim_that_is_not_Approved_gets_409()
    {
        var claim = await PickedUpBy(Priya);

        var response = await factory.ClientAs(Priya).PostAsync($"/api/claims/{claim.Id}/settle", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Only an Approved Claim can be settled.", await Detail(response));
    }

    [Theory]
    [InlineData("""{ "amount": 0 }""")]
    [InlineData("""{ "amount": -10 }""")]
    [InlineData("""{ }""")]
    [InlineData("""{ "amount": "lots" }""")]
    public async Task A_Reserve_that_is_missing_not_a_number_or_not_greater_than_zero_gets_400(string body)
    {
        var claim = await PickedUpBy(Priya);

        var response = await Send(Priya, "PUT", claim.Id, "reserve", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "reason": "" }""")]
    [InlineData("""{ "reason": "   " }""")]
    public async Task Rejecting_without_a_reason_gets_400(string body)
    {
        var claim = await PickedUpBy(Priya);

        var response = await Send(Priya, "POST", claim.Id, "reject", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("UnderReview", (await factory.ClientAs(Priya).GetFromJsonAsync<ClaimDto>($"/api/claims/{claim.Id}"))!.Status);
    }

    [Fact]
    public async Task Acting_on_a_Claim_that_does_not_exist_gets_404()
    {
        var response = await factory.ClientAs(Priya).PostAsync($"/api/claims/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record QueueItemDto(Guid Id);

    [Fact]
    public async Task Queue_leaves_out_the_Officers_Settled_and_Rejected_Claims()
    {
        var settled = await SettledBy(Priya);
        var rejected = await PickedUpBy(Priya);
        (await Send(Priya, "POST", rejected.Id, "reject", """{ "reason": "Not covered" }""")).EnsureSuccessStatusCode();
        var stillOpen = await PickedUpBy(Priya);

        var queue = (await factory.ClientAs(Priya).GetFromJsonAsync<List<QueueItemDto>>("/api/queue"))!;

        Assert.DoesNotContain(queue, c => c.Id == settled.Id || c.Id == rejected.Id);
        Assert.Contains(queue, c => c.Id == stillOpen.Id);
    }

    private async Task<ClaimDto> PickedUpBy(DemoUser officer, DemoUser? claimant = null)
    {
        var claim = await SubmitAs(claimant ?? Aisha);
        (await factory.ClientAs(officer).PostAsync($"/api/claims/{claim.Id}/pick-up", null)).EnsureSuccessStatusCode();
        return claim;
    }

    private async Task<ClaimDto> SettledBy(DemoUser officer)
    {
        var claim = await PickedUpBy(officer);
        (await Send(officer, "PUT", claim.Id, "reserve", """{ "amount": 800 }""")).EnsureSuccessStatusCode();
        (await Send(officer, "POST", claim.Id, "approve", null)).EnsureSuccessStatusCode();
        (await Send(officer, "POST", claim.Id, "settle", null)).EnsureSuccessStatusCode();
        return claim;
    }

    private Task<HttpResponseMessage> Send(DemoUser user, string method, Guid claimId, string action, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"/api/claims/{claimId}/{action}");
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return factory.ClientAs(user).SendAsync(request);
    }

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

    private async Task<ClaimDto> SubmitAs(DemoUser claimant, string market = "SG")
    {
        var claim = new
        {
            policyNumber = "POL-123", market, claimType = "Property",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            description = "Burst pipe flooded the kitchen",
        };
        var response = await factory.ClientAs(claimant).PostAsJsonAsync("/api/claims", claim);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }
}
