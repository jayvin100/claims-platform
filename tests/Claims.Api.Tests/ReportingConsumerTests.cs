using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Outbox;
using Claims.Api.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>
/// The Reporting read model's tests share one table and add up every row in it, so they run one at a time
/// (xUnit runs a collection's classes in sequence) and each test starts from an empty table.
/// </summary>
[CollectionDefinition(Name)]
public class ReportingCollection
{
    public const string Name = "Reporting read model";
}

/// <summary>
/// Seam 3: events are handed straight to the Reporting consumer's processing method, against the test database,
/// with no Kafka. Each event goes through its outbox row, so the consumer reads exactly the type and JSON it
/// would receive from the topic.
/// </summary>
[Collection(ReportingCollection.Name)]
public class ReportingConsumerTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 2, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => ReportingTestData.ClearReadModel(factory);
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ClaimSubmitted_adds_one_row_with_its_currency_and_no_Officer_or_Reserve()
    {
        var claim = new TestClaim(Market.MY);

        await Apply(claim.Submitted());

        var row = await RowFor(claim.Id);
        Assert.Equal((Market.MY, "MYR", ClaimStatus.Submitted), (row.Market, row.Currency, row.Status));
        Assert.Null(row.AssignedOfficerId);
        Assert.Null(row.ReserveAmount);
    }

    [Fact]
    public async Task The_currency_comes_from_ClaimSubmitted_and_ReserveSet()
    {
        // Currencies that aren't the Market's, so the test fails if the row falls back to the Market's (D7).
        var claim = new TestClaim(Market.SG);

        await Apply(claim.Submitted(currency: "USD"));
        Assert.Equal("USD", (await RowFor(claim.Id)).Currency);

        await Apply(claim.PickedUp(Priya.Id), claim.ReserveSet(1000m, currency: "EUR"));
        Assert.Equal("EUR", (await RowFor(claim.Id)).Currency);
    }

    [Fact]
    public async Task The_same_event_applied_twice_counts_once_and_is_recorded_once()
    {
        var claim = new TestClaim(Market.SG);
        var infoRequested = claim.InfoRequested();
        await Apply(claim.Submitted(), claim.PickedUp(Priya.Id), infoRequested, claim.InfoProvided());

        // Without the processed-id check, this would move the Claim back to Info Requested.
        await Apply(infoRequested);

        await AssertPriyaHolds(underReview: 1);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.ProcessedEvents.CountAsync(p => p.Consumer == ReportingConsumer.Name && p.EventId == infoRequested.EventId));
    }

    [Fact]
    public async Task An_old_duplicate_arriving_after_settlement_does_not_bring_the_Reserve_back()
    {
        var claim = new TestClaim(Market.SG);
        var pickedUp = claim.PickedUp(Priya.Id);
        var reserveSet = claim.ReserveSet(1000m);
        await Apply(claim.Submitted(), pickedUp, reserveSet, claim.Approved(), claim.Settled());

        // Kafka redelivers two old events, e.g. after a crash before the offset was committed.
        await Apply(pickedUp, reserveSet);

        var sg = await ExposureFor(Market.SG);
        Assert.Equal((0, 0m, 0), (sg.OpenClaims, sg.OpenReserves, sg.UnreservedClaims));
        Assert.Equal(ClaimStatus.Settled, (await RowFor(claim.Id)).Status);
    }

    [Fact]
    public async Task Open_Reserves_exclude_Settled_and_Rejected_Claims_and_keep_each_Market_in_its_own_currency()
    {
        var sgOpen = new TestClaim(Market.SG);
        var sgUnreserved = new TestClaim(Market.SG);
        var sgSettled = new TestClaim(Market.SG);
        var sgRejected = new TestClaim(Market.SG);
        var myOpen = new TestClaim(Market.MY);
        await Apply(sgOpen.Submitted(), sgOpen.PickedUp(Priya.Id), sgOpen.ReserveSet(1000m));
        await Apply(sgUnreserved.Submitted());
        await Apply(sgSettled.Submitted(), sgSettled.PickedUp(Priya.Id), sgSettled.ReserveSet(500m), sgSettled.Approved(), sgSettled.Settled());
        await Apply(sgRejected.Submitted(), sgRejected.PickedUp(Marcus.Id), sgRejected.ReserveSet(200m), sgRejected.Rejected());
        await Apply(myOpen.Submitted(), myOpen.PickedUp(Marcus.Id), myOpen.ReserveSet(3000m), myOpen.Approved());

        var exposure = await ExposureNow();

        // Every Market is listed, each in its own currency, so the six rows never add up into one figure.
        Assert.Equal(Enum.GetValues<Market>(), exposure.Select(e => e.Market));
        Assert.All(exposure, e => Assert.Equal(Markets.CurrencyOf(e.Market), e.Currency));
        var sg = exposure.Single(e => e.Market == Market.SG);
        Assert.Equal((2, 1000m, 1), (sg.OpenClaims, sg.OpenReserves, sg.UnreservedClaims));
        var my = exposure.Single(e => e.Market == Market.MY);
        Assert.Equal((1, 3000m, 0), (my.OpenClaims, my.OpenReserves, my.UnreservedClaims));
        Assert.All(exposure.Where(e => e.Market is not (Market.SG or Market.MY)),
            e => Assert.Equal((0, 0m, 0), (e.OpenClaims, e.OpenReserves, e.UnreservedClaims)));
    }

    [Fact]
    public async Task A_revised_Reserve_replaces_the_earlier_one()
    {
        var claim = new TestClaim(Market.HK);

        await Apply(claim.Submitted(), claim.PickedUp(Priya.Id), claim.ReserveSet(1000m), claim.ReserveSet(1250.75m));

        var hk = await ExposureFor(Market.HK);
        Assert.Equal((1, 1250.75m, 0), (hk.OpenClaims, hk.OpenReserves, hk.UnreservedClaims));
    }

    [Fact]
    public async Task Workload_counts_move_with_a_Claim_through_its_full_lifecycle()
    {
        var claim = new TestClaim(Market.SG);

        await Apply(claim.Submitted());
        var workload = await WorkloadNow();
        Assert.Equal(1, workload.UnassignedSubmitted);
        Assert.Empty(workload.Officers);

        await Apply(claim.PickedUp(Priya.Id));
        await AssertPriyaHolds(underReview: 1);

        await Apply(claim.ReserveSet(900m));
        await AssertPriyaHolds(underReview: 1);

        await Apply(claim.InfoRequested());
        await AssertPriyaHolds(infoRequested: 1);

        await Apply(claim.InfoProvided());
        await AssertPriyaHolds(underReview: 1);

        await Apply(claim.Approved());
        await AssertPriyaHolds(approved: 1);

        await Apply(claim.Settled());
        await AssertPriyaHolds(settled: 1);

        var rejected = new TestClaim(Market.TH);
        await Apply(rejected.Submitted(), rejected.PickedUp(Priya.Id), rejected.Rejected());
        await AssertPriyaHolds(settled: 1, rejected: 1);
    }

    [Fact]
    public async Task Workload_lists_each_Officer_separately()
    {
        var priyas = new TestClaim(Market.SG);
        var marcuses = new TestClaim(Market.SG);
        await Apply(priyas.Submitted(), priyas.PickedUp(Priya.Id));
        await Apply(marcuses.Submitted(), marcuses.PickedUp(Marcus.Id), marcuses.InfoRequested());

        var workload = await WorkloadNow();

        Assert.Equal(0, workload.UnassignedSubmitted);
        Assert.Equal(new OfficerWorkloadResponse(Priya.Id, 1, 0, 0, 0, 0), workload.Officers.Single(o => o.OfficerId == Priya.Id));
        Assert.Equal(new OfficerWorkloadResponse(Marcus.Id, 0, 1, 0, 0, 0), workload.Officers.Single(o => o.OfficerId == Marcus.Id));
    }

    [Fact]
    public async Task An_unknown_event_type_throws_and_is_not_recorded_as_processed()
    {
        var eventId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Consumer().ProcessAsync(
            "ClaimArchived", $$"""{ "eventId": "{{eventId}}", "claimId": "{{Guid.NewGuid()}}" }""", CancellationToken.None));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProcessedEvents.AnyAsync(p => p.Consumer == ReportingConsumer.Name && p.EventId == eventId));
        Assert.Equal(0, await db.ReportingClaims.CountAsync());
    }

    // An event type Reporting has no rule for blocks its partition (see ReportingConsumer), so this test is what
    // catches a new event type before it ships.
    [Fact]
    public async Task Reporting_handles_every_event_type()
    {
        var claim = new TestClaim(Market.SG);
        ClaimEvent[] oneOfEach =
        [
            claim.Submitted(), claim.PickedUp(Priya.Id), claim.ReserveSet(100m), claim.InfoRequested(),
            claim.InfoProvided(), claim.Approved(), claim.Rejected(), claim.Settled(),
        ];
        var everyEventType = typeof(ClaimEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ClaimEvent)))
            .Select(t => t.Name);
        // A new event type fails here until it is added to oneOfEach, and so is applied below.
        Assert.Equal(everyEventType.Order(), oneOfEach.Select(e => e.GetType().Name).Order());

        await Apply(oneOfEach); // throws for any type Reporting has no rule for

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = oneOfEach.Select(e => e.EventId).ToList();
        Assert.Equal(oneOfEach.Length, await db.ProcessedEvents.CountAsync(p => p.Consumer == ReportingConsumer.Name && ids.Contains(p.EventId)));
    }

    private async Task AssertPriyaHolds(int underReview = 0, int infoRequested = 0, int approved = 0, int settled = 0, int rejected = 0)
    {
        var workload = await WorkloadNow();
        Assert.Equal(0, workload.UnassignedSubmitted);
        Assert.Equal(new OfficerWorkloadResponse(Priya.Id, underReview, infoRequested, approved, settled, rejected),
            Assert.Single(workload.Officers));
    }

    private ReportingConsumer Consumer() => ActivatorUtilities.CreateInstance<ReportingConsumer>(factory.Services);

    private async Task Apply(params ClaimEvent[] events)
    {
        var consumer = Consumer();
        foreach (var claimEvent in events)
        {
            var row = OutboxMessage.From(claimEvent);
            await consumer.ProcessAsync(row.Type, row.Payload, CancellationToken.None);
        }
    }

    private async Task<ReportingClaim> RowFor(Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ReportingClaims.SingleAsync(r => r.ClaimId == claimId);
    }

    private async Task<List<MarketExposureResponse>> ExposureNow()
    {
        using var scope = factory.Services.CreateScope();
        return await ReportingQueries.Exposure(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<MarketExposureResponse> ExposureFor(Market market) => (await ExposureNow()).Single(e => e.Market == market);

    private async Task<WorkloadResponse> WorkloadNow()
    {
        using var scope = factory.Services.CreateScope();
        return await ReportingQueries.Workload(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Builds one Claim's events, each with a new event id, as the Claim would raise them.</summary>
    private class TestClaim(Market market)
    {
        public Guid Id { get; } = Guid.NewGuid();
        private readonly string _number = $"CLM-{market}-{Random.Shared.Next(1_000_000):D6}";

        public ClaimSubmitted Submitted(string? currency = null) =>
            new(Guid.NewGuid(), Id, _number, market, Now, Aisha.Id, currency ?? Markets.CurrencyOf(market));
        public ClaimPickedUp PickedUp(Guid officerId) => new(Guid.NewGuid(), Id, _number, market, Now, officerId, Aisha.Id);
        public ReserveSet ReserveSet(decimal amount, string? currency = null) =>
            new(Guid.NewGuid(), Id, _number, market, Now, amount, currency ?? Markets.CurrencyOf(market));
        public InfoRequested InfoRequested() => new(Guid.NewGuid(), Id, _number, market, Now, Aisha.Id, "Please send the repair quote.");
        public InfoProvided InfoProvided() => new(Guid.NewGuid(), Id, _number, market, Now);
        public ClaimApproved Approved() => new(Guid.NewGuid(), Id, _number, market, Now, Aisha.Id);
        public ClaimRejected Rejected() => new(Guid.NewGuid(), Id, _number, market, Now, Aisha.Id, "Not covered");
        public ClaimSettled Settled() => new(Guid.NewGuid(), Id, _number, market, Now, Aisha.Id);
    }
}

public static class ReportingTestData
{
    /// <summary>Empties the read model, so a test's totals count only its own Claims.</summary>
    public static async Task ClearReadModel(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().ReportingClaims.ExecuteDeleteAsync();
    }
}
