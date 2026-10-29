using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>
/// Seam 1: the Workload and Exposure endpoints. Kafka is off in tests, so each test feeds the Claim's real
/// outbox rows to the Reporting consumer by hand, standing in for the topic.
/// </summary>
[Collection(ReportingCollection.Name)]
public class ReportsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => ReportingTestData.ClearReadModel(factory);
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Exposure_drops_when_a_Claim_is_settled()
    {
        var claim = await SubmitAs(Aisha, "SG");
        (await Send(Priya, "POST", claim, "pick-up", null)).EnsureSuccessStatusCode();
        (await Send(Priya, "PUT", claim, "reserve", """{ "amount": 1500.50 }""")).EnsureSuccessStatusCode();
        (await Send(Priya, "POST", claim, "approve", null)).EnsureSuccessStatusCode();
        await DeliverOutboxFor(claim);

        var before = await ExposureAs(Priya);
        (await Send(Priya, "POST", claim, "settle", null)).EnsureSuccessStatusCode();
        await DeliverOutboxFor(claim);
        var after = await ExposureAs(Priya);

        Assert.Equal((1, 1500.50m, 0), (before["SG"].OpenClaims, before["SG"].OpenReserves, before["SG"].UnreservedClaims));
        Assert.Equal((0, 0m, 0), (after["SG"].OpenClaims, after["SG"].OpenReserves, after["SG"].UnreservedClaims));
    }

    [Fact]
    public async Task Exposure_lists_every_Market_with_its_currency_and_counts_Unreserved_Claims()
    {
        var unreserved = await SubmitAs(Daniel, "ID");
        await DeliverOutboxFor(unreserved);

        var response = await factory.ClientAs(Marcus).GetAsync("/api/reports/exposure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["SG", "MY", "HK", "TH", "ID", "PH"], json.EnumerateArray().Select(m => m.GetProperty("market").GetString()));
        var id = json.EnumerateArray().Single(m => m.GetProperty("market").GetString() == "ID");
        Assert.Equal("IDR", id.GetProperty("currency").GetString());
        Assert.Equal(1, id.GetProperty("openClaims").GetInt32());
        Assert.Equal(0m, id.GetProperty("openReserves").GetDecimal());
        Assert.Equal(1, id.GetProperty("unreservedClaims").GetInt32());
    }

    [Fact]
    public async Task Workload_shows_each_Officers_Claims_by_status_and_the_unassigned_count()
    {
        var unassigned = await SubmitAs(Aisha, "PH");
        var priyas = await SubmitAs(Aisha, "SG");
        (await Send(Priya, "POST", priyas, "pick-up", null)).EnsureSuccessStatusCode();
        (await Send(Priya, "POST", priyas, "info-requests", """{ "question": "Please send the repair quote." }""")).EnsureSuccessStatusCode();
        var marcuses = await SubmitAs(Daniel, "HK");
        (await Send(Marcus, "POST", marcuses, "pick-up", null)).EnsureSuccessStatusCode();
        (await Send(Marcus, "POST", marcuses, "reject", """{ "reason": "Not covered" }""")).EnsureSuccessStatusCode();
        await DeliverOutboxFor(unassigned, priyas, marcuses);

        var response = await factory.ClientAs(Priya).GetAsync("/api/reports/workload");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("unassignedSubmitted").GetInt32());
        var officers = json.GetProperty("officers").EnumerateArray()
            .ToDictionary(o => o.GetProperty("officerId").GetGuid(), o => o.ToString());
        Assert.Equal(2, officers.Count);
        Assert.Equal(Normalise($$"""{"officerId":"{{Priya.Id}}","underReview":0,"infoRequested":1,"approved":0,"settled":0,"rejected":0}"""),
            Normalise(officers[Priya.Id]));
        Assert.Equal(Normalise($$"""{"officerId":"{{Marcus.Id}}","underReview":0,"infoRequested":0,"approved":0,"settled":0,"rejected":1}"""),
            Normalise(officers[Marcus.Id]));
    }

    [Theory]
    [InlineData("/api/reports/exposure")]
    [InlineData("/api/reports/workload")]
    public async Task A_Claimant_is_refused_the_reports_with_403(string url)
    {
        var response = await factory.ClientAs(Aisha).GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/reports/exposure")]
    [InlineData("/api/reports/workload")]
    public async Task A_request_with_no_user_is_refused_the_reports_with_401(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string Normalise(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private record ExposureDto(string Market, string Currency, int OpenClaims, decimal OpenReserves, int UnreservedClaims);

    private async Task<Dictionary<string, ExposureDto>> ExposureAs(DemoUser officer) =>
        (await factory.ClientAs(officer).GetFromJsonAsync<List<ExposureDto>>("/api/reports/exposure"))!
            .ToDictionary(e => e.Market);

    // Stands in for Kafka: hands each of the Claims' outbox rows to the Reporting consumer, in sequence order.
    // Rows delivered before are skipped as already processed, as a redelivery would be.
    private async Task DeliverOutboxFor(params Guid[] claimIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.OutboxMessages.Where(m => claimIds.Contains(m.ClaimId)).OrderBy(m => m.Sequence).ToListAsync();
        var consumer = ActivatorUtilities.CreateInstance<ReportingConsumer>(factory.Services);
        foreach (var row in rows)
            await consumer.ProcessAsync(row.Type, row.Payload, CancellationToken.None);
    }

    private Task<HttpResponseMessage> Send(DemoUser user, string method, Guid claimId, string action, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"/api/claims/{claimId}/{action}");
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return factory.ClientAs(user).SendAsync(request);
    }

    private async Task<Guid> SubmitAs(DemoUser claimant, string market)
    {
        var claim = new
        {
            policyNumber = "POL-123", market, claimType = "Motor",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            description = "Rear-ended at a junction",
        };
        var response = await factory.ClientAs(claimant).PostAsJsonAsync("/api/claims", claim);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
