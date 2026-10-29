using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.Tests;

/// <summary>
/// Runs the real API in memory against its own database (claims_test) on the Compose Postgres,
/// so tests never touch the demo data. Development mode means startup applies migrations and seeds.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Host=localhost;Port=5433;Database=claims_test;Username=claims;Password=claims";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Claims", TestConnectionString);

        // Adds the test-only WhoAmIController alongside the API's own controllers.
        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly));
    }
}
