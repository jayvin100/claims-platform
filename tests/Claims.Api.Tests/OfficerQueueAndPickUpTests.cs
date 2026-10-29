using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Claims.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>Seam 1: an Officer works their Queue and picks up Claims through the HTTP API.</summary>
public class OfficerQueueAndPickUpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record HistoryDto(string Status, string Description);

    private record ClaimDto(Guid Id, string ClaimNumber, string Status, Guid ClaimantId, Guid? AssignedOfficerId, List<HistoryDto> History);

    [Fact]
    public async Task Officer_picks_up_a_Submitted_Claim_and_gets_it_back_Under_Review_as_its_Assigned_Officer()
    {
        var submitted = await factory.SubmitAs(Aisha);

        var response = await factory.ClientAs(Priya).PostAsync($"/api/claims/{submitted.Id}/pick-up", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var claim = (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
        Assert.Equal("UnderReview", claim.Status);
        Assert.Equal(Priya.Id, claim.AssignedOfficerId);
        Assert.Equal(("UnderReview", "Claim picked up for review"), (claim.History[^1].Status, claim.History[^1].Description));
    }

    [Fact]
    public async Task Picking_up_a_Claim_that_is_no_longer_Submitted_is_refused_with_409_saying_it_is_already_picked_up()
    {
        var submitted = await factory.SubmitAs(Aisha);
        await PickUpAs(Priya, submitted.Id);

        var response = await PickUpAs(Marcus, submitted.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("This Claim has already been picked up.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Two_Officers_picking_up_the_same_Claim_at_once_one_succeeds_and_the_other_gets_409()
    {
        var submitted = await factory.SubmitAs(Aisha);

        // Whichever lands second fails either the status rule or, if both loaded it as Submitted, the xmin check.
        var responses = await Task.WhenAll(PickUpAs(Priya, submitted.Id), PickUpAs(Marcus, submitted.Id));

        var codes = responses.Select(r => r.StatusCode).Order().ToList();
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], codes);
        var winner = (await responses.Single(r => r.IsSuccessStatusCode).Content.ReadFromJsonAsync<ClaimDto>())!;
        var saved = await factory.ClientAs(Priya).GetFromJsonAsync<ClaimDto>($"/api/claims/{submitted.Id}");
        Assert.Equal(winner.AssignedOfficerId, saved!.AssignedOfficerId);
        Assert.Single(saved.History, h => h.Status == "UnderReview");
    }

    [Fact]
    public async Task Picking_up_a_Claim_that_does_not_exist_gets_404()
    {
        var response = await PickUpAs(Priya, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Picking_up_a_Claim_writes_ClaimPickedUp_with_the_Assigned_Officer_and_Claimant_to_the_outbox()
    {
        var submitted = await factory.SubmitAs(Daniel);

        (await PickUpAs(Marcus, submitted.Id)).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.OutboxMessages.Where(m => m.ClaimId == submitted.Id).OrderBy(m => m.Sequence).ToListAsync();
        Assert.Equal(["ClaimSubmitted", "ClaimPickedUp"], rows.Select(r => r.Type));
        using var payload = JsonDocument.Parse(rows[1].Payload);
        var json = payload.RootElement;
        Assert.Equal(rows[1].EventId, json.GetProperty("eventId").GetGuid());
        Assert.Equal(submitted.ClaimNumber, json.GetProperty("claimNumber").GetString());
        Assert.Equal("SG", json.GetProperty("market").GetString());
        Assert.Equal(Marcus.Id, json.GetProperty("assignedOfficerId").GetGuid());
        Assert.Equal(Daniel.Id, json.GetProperty("claimantId").GetGuid());
    }

    [Theory]
    [InlineData("POST", "/api/claims/{id}/pick-up")]
    [InlineData("GET", "/api/queue")]
    public async Task Claimant_calling_an_Officer_endpoint_is_refused_with_403(string method, string path)
    {
        var submitted = await factory.SubmitAs(Aisha);

        var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", submitted.Id.ToString()));
        var response = await factory.ClientAs(Aisha).SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private record QueueItemDto(Guid Id, string ClaimNumber, string Market, string ClaimType, string Status, DateTimeOffset CreatedAt, bool IsMine);

    [Fact]
    public async Task Queue_has_unassigned_Submitted_Claims_and_the_Officers_own_Claims_oldest_first()
    {
        var mine = await factory.SubmitAs(Aisha);
        var unassigned = await factory.SubmitAs(Daniel);
        (await PickUpAs(Priya, mine.Id)).EnsureSuccessStatusCode();

        var queue = (await factory.ClientAs(Priya).GetFromJsonAsync<List<QueueItemDto>>("/api/queue"))!;

        var ids = queue.Select(c => c.Id).ToList();
        Assert.True(ids.IndexOf(mine.Id) >= 0 && ids.IndexOf(mine.Id) < ids.IndexOf(unassigned.Id), "the older Claim should come first");
        Assert.Equal(queue.OrderBy(c => c.CreatedAt).Select(c => c.Id), ids);
        var mineRow = queue.Single(c => c.Id == mine.Id);
        Assert.Equal((mine.ClaimNumber, "SG", "Motor", "UnderReview", true),
            (mineRow.ClaimNumber, mineRow.Market, mineRow.ClaimType, mineRow.Status, mineRow.IsMine));
        var unassignedRow = queue.Single(c => c.Id == unassigned.Id);
        Assert.Equal(("Submitted", false), (unassignedRow.Status, unassignedRow.IsMine));
    }

    [Fact]
    public async Task Queue_leaves_out_Claims_held_by_other_Officers()
    {
        var marcusClaim = await factory.SubmitAs(Aisha);
        (await PickUpAs(Marcus, marcusClaim.Id)).EnsureSuccessStatusCode();

        var queue = (await factory.ClientAs(Priya).GetFromJsonAsync<List<QueueItemDto>>("/api/queue"))!;

        Assert.DoesNotContain(queue, c => c.Id == marcusClaim.Id);
        Assert.All(queue, c => Assert.True(c.Status == "Submitted" || c.IsMine));
    }

    private Task<HttpResponseMessage> PickUpAs(DemoUser officer, Guid claimId) =>
        factory.ClientAs(officer).PostAsync($"/api/claims/{claimId}/pick-up", null);
}
