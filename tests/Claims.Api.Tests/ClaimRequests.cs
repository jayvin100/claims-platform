using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Claims.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>The Claim requests the HTTP API tests share: submit, pick up, act on a Claim, and read what came back.</summary>
public static class ClaimRequests
{
    /// <summary>The Id and Claim Number of a Claim a test submitted; each test class reads the rest with its own DTO.</summary>
    public record SubmittedClaim(Guid Id, string ClaimNumber);

    public static object ValidClaim(string market = "SG") => new
    {
        policyNumber = "POL-123",
        market,
        claimType = "Motor",
        incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
        description = "Rear-ended at a junction",
    };

    public static async Task<SubmittedClaim> SubmitAs(this ApiFactory factory, DemoUser claimant, string market = "SG")
    {
        var response = await factory.ClientAs(claimant).PostAsJsonAsync("/api/claims", ValidClaim(market));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmittedClaim>())!;
    }

    public static async Task<SubmittedClaim> PickedUpBy(this ApiFactory factory, DemoUser officer, DemoUser? claimant = null)
    {
        var claim = await factory.SubmitAs(claimant ?? Aisha);
        (await factory.ClientAs(officer).PostAsync($"/api/claims/{claim.Id}/pick-up", null)).EnsureSuccessStatusCode();
        return claim;
    }

    /// <summary>Sends an action on a Claim, e.g. PUT reserve, with an optional JSON body.</summary>
    public static Task<HttpResponseMessage> Send(this ApiFactory factory, DemoUser user, string method, Guid claimId, string action, string? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"/api/claims/{claimId}/{action}");
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return factory.ClientAs(user).SendAsync(request);
    }

    /// <summary>The Claim's outbox rows in sequence order, as the event type and its JSON payload.</summary>
    public static async Task<List<(string Type, JsonElement Json)>> OutboxFor(this ApiFactory factory, Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.OutboxMessages.Where(m => m.ClaimId == claimId).OrderBy(m => m.Sequence).ToListAsync();
        // Clone so each payload outlives its JsonDocument.
        return rows.Select(r => (r.Type, JsonDocument.Parse(r.Payload).RootElement.Clone())).ToList();
    }

    /// <summary>The problem details' "detail" message, which names the rule that refused the request.</summary>
    public static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    /// <summary>Checks the response is 200 OK and reads the Claim into the test class's own DTO.</summary>
    public static async Task<T> ReadClaim<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
