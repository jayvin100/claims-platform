using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Claims.Api.Tests;

/// <summary>
/// Runs the real API in memory against its own database (claims_test) on the Compose Postgres,
/// so tests never touch the demo data. Development mode means startup applies migrations and seeds.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // CLAIMS_TEST_DB names another test database, so work in a second git worktree can run its tests
    // without breaking these. Migrate creates the database if it doesn't exist.
    public static readonly string TestConnectionString =
        $"Host=localhost;Port=5433;Database={Environment.GetEnvironmentVariable("CLAIMS_TEST_DB") ?? "claims_test"};Username=claims;Password=claims";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Claims", TestConnectionString);

        // Adds the test-only WhoAmIController alongside the API's own controllers.
        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly));
    }

    private static readonly Lock StartLock = new();

    // xUnit runs test classes in parallel, each with its own ApiFactory, and each host applies migrations
    // as it starts. On a new database they would race to create the same tables, so hosts start one at a time.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (StartLock)
            return base.CreateHost(builder);
    }
}
