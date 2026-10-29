using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Claims.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.Tests;

/// <summary>Seam 1: a Claimant submits and tracks Claims through the HTTP API.</summary>
public class SubmitAndTrackClaimTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record HistoryDto(string Status, string Description, DateTimeOffset OccurredAt);

    private record ClaimDto(
        Guid Id, string ClaimNumber, string PolicyNumber, string Market, string Currency, string ClaimType,
        DateOnly IncidentDate, string Description, string Status, Guid ClaimantId,
        DateTimeOffset CreatedAt, List<HistoryDto> History);

    [Fact]
    public async Task Claimant_submits_a_Claim_and_gets_back_a_Submitted_Claim_with_a_Claim_Number()
    {
        var response = await As(Aisha).PostAsJsonAsync("/api/claims", ValidClaim(market: "HK"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var claim = (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
        Assert.Matches(new Regex(@"^CLM-HK-\d{6}$"), claim.ClaimNumber);
        Assert.Equal("Submitted", claim.Status);
        Assert.Equal("HKD", claim.Currency);
        Assert.Equal(Aisha.Id, claim.ClaimantId);
        Assert.Equal("Claim submitted", Assert.Single(claim.History).Description);
        Assert.Equal($"/api/claims/{claim.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Submitting_a_Claim_writes_ClaimSubmitted_to_the_outbox()
    {
        var response = await As(Aisha).PostAsJsonAsync("/api/claims", ValidClaim(market: "TH"));
        var claim = (await response.Content.ReadFromJsonAsync<ClaimDto>())!;

        // The outbox is what the publisher (and so every consumer) will read, so the test reads it too.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.OutboxMessages.SingleAsync(m => m.ClaimId == claim.Id);

        Assert.Equal("ClaimSubmitted", row.Type);
        Assert.True(row.Sequence > 0);
        Assert.Null(row.SentAt);
        using var payload = JsonDocument.Parse(row.Payload);
        var json = payload.RootElement;
        Assert.Equal(row.EventId, json.GetProperty("eventId").GetGuid());
        Assert.Equal(claim.Id, json.GetProperty("claimId").GetGuid());
        Assert.Equal(claim.ClaimNumber, json.GetProperty("claimNumber").GetString());
        Assert.Equal("TH", json.GetProperty("market").GetString());
        Assert.Equal(claim.CreatedAt, json.GetProperty("occurredAt").GetDateTimeOffset());
        Assert.Equal(Aisha.Id, json.GetProperty("claimantId").GetGuid());
        Assert.Equal("THB", json.GetProperty("currency").GetString());
        Assert.Equal(claim.CreatedAt, row.OccurredAt, PostgresTimePrecision);
    }

    [Theory]
    [InlineData("""{ "market": "SG", "claimType": "Motor", "incidentDate": "2026-01-01", "description": "x" }""", "no Policy Number")]
    [InlineData("""{ "policyNumber": "P", "claimType": "Motor", "incidentDate": "2026-01-01", "description": "x" }""", "no Market")]
    [InlineData("""{ "policyNumber": "P", "market": "US", "claimType": "Motor", "incidentDate": "2026-01-01", "description": "x" }""", "not one of the six Markets")]
    [InlineData("""{ "policyNumber": "P", "market": 0, "claimType": "Motor", "incidentDate": "2026-01-01", "description": "x" }""", "Market as a number")]
    [InlineData("""{ "policyNumber": "P", "market": "SG", "claimType": "Travel", "incidentDate": "2026-01-01", "description": "x" }""", "unknown Claim Type")]
    [InlineData("""{ "policyNumber": "P", "market": "SG", "claimType": "Motor", "description": "x" }""", "no incident date")]
    [InlineData("""{ "policyNumber": "P", "market": "SG", "claimType": "Motor", "incidentDate": "2026-01-01", "description": "" }""", "empty description")]
    public async Task Submitting_a_Claim_with_a_missing_or_invalid_field_is_refused_with_400(string body, string because)
    {
        var response = await As(Aisha).PostAsync("/api/claims", new StringContent(body, Encoding.UTF8, "application/json"));

        _ = because; // names the case in the test runner output
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submitting_a_Claim_with_an_incident_date_in_the_future_is_refused_with_400()
    {
        var claim = new
        {
            policyNumber = "POL-123", market = "SG", claimType = "Motor",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            description = "Not yet happened",
        };

        var response = await As(Aisha).PostAsJsonAsync("/api/claims", claim);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("The incident date can't be in the future.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Currency_sent_by_the_Claimant_is_ignored_and_taken_from_the_Market()
    {
        var claim = new
        {
            policyNumber = "POL-123", market = "PH", claimType = "Property",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            description = "Burst pipe", currency = "USD",
        };

        var response = await As(Aisha).PostAsJsonAsync("/api/claims", claim);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PHP", (await response.Content.ReadFromJsonAsync<ClaimDto>())!.Currency);
    }

    [Fact]
    public async Task Officer_submitting_a_Claim_is_refused_with_403()
    {
        var response = await As(Priya).PostAsJsonAsync("/api/claims", ValidClaim());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private record ClaimSummaryDto(Guid Id, string ClaimNumber, string Market, string ClaimType, string Status, DateTimeOffset CreatedAt);

    [Fact]
    public async Task Claimant_lists_only_their_own_Claims_newest_first()
    {
        var older = await SubmitAs(Aisha);
        var danielsClaim = await SubmitAs(Daniel);
        var newer = await SubmitAs(Aisha);

        var claims = (await As(Aisha).GetFromJsonAsync<List<ClaimSummaryDto>>("/api/claims"))!;

        var ids = claims.Select(c => c.Id).ToList();
        Assert.Contains(older.Id, ids);
        Assert.Contains(newer.Id, ids);
        Assert.True(ids.IndexOf(newer.Id) < ids.IndexOf(older.Id), "the newer Claim should come first");
        Assert.DoesNotContain(danielsClaim.Id, ids);
        Assert.Equal(claims.OrderByDescending(c => c.CreatedAt).Select(c => c.Id), ids);
        var listed = claims.Single(c => c.Id == newer.Id);
        Assert.Equal((newer.ClaimNumber, "SG", "Motor", "Submitted"), (listed.ClaimNumber, listed.Market, listed.ClaimType, listed.Status));
    }

    [Fact]
    public async Task Officer_listing_Claimant_Claims_is_refused_with_403()
    {
        var response = await As(Priya).GetAsync("/api/claims");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Claimant_opens_their_Claim_and_sees_its_details_and_Claim_History()
    {
        var submitted = await SubmitAs(Aisha);

        var claim = (await As(Aisha).GetFromJsonAsync<ClaimDto>($"/api/claims/{submitted.Id}"))!;

        Assert.Equal(submitted.ClaimNumber, claim.ClaimNumber);
        Assert.Equal("POL-123", claim.PolicyNumber);
        Assert.Equal("Rear-ended at a junction", claim.Description);
        var entry = Assert.Single(claim.History);
        Assert.Equal(("Submitted", "Claim submitted"), (entry.Status, entry.Description));
    }

    [Fact]
    public async Task Claimant_opening_another_Claimants_Claim_gets_404()
    {
        var danielsClaim = await SubmitAs(Daniel);

        var response = await As(Aisha).GetAsync($"/api/claims/{danielsClaim.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Opening_a_Claim_that_does_not_exist_gets_404()
    {
        var response = await As(Aisha).GetAsync($"/api/claims/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Officer_can_open_any_Claim()
    {
        var danielsClaim = await SubmitAs(Daniel);

        var claim = (await As(Priya).GetFromJsonAsync<ClaimDto>($"/api/claims/{danielsClaim.Id}"))!;

        Assert.Equal(danielsClaim.ClaimNumber, claim.ClaimNumber);
    }

    private async Task<ClaimDto> SubmitAs(DemoIdentity claimant)
    {
        var response = await As(claimant).PostAsJsonAsync("/api/claims", ValidClaim());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    private static object ValidClaim(string market = "SG") => new
    {
        policyNumber = "POL-123",
        market,
        claimType = "Motor",
        incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
        description = "Rear-ended at a junction",
    };

    // Postgres stores microseconds; .NET times can carry finer ticks that are lost on the way in.
    private static readonly TimeSpan PostgresTimePrecision = TimeSpan.FromMicroseconds(1);

    private record DemoIdentity(Guid Id, string Role);

    private static readonly DemoIdentity Aisha = new(Guid.Parse("11111111-0000-0000-0000-000000000001"), "Claimant");
    private static readonly DemoIdentity Daniel = new(Guid.Parse("11111111-0000-0000-0000-000000000002"), "Claimant");
    private static readonly DemoIdentity Priya = new(Guid.Parse("22222222-0000-0000-0000-000000000001"), "Officer");

    private HttpClient As(DemoIdentity user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", user.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Role", user.Role);
        return client;
    }
}
