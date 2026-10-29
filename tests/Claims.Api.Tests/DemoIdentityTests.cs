using System.Net;
using System.Net.Http.Json;

namespace Claims.Api.Tests;

public class DemoIdentityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record UserDto(Guid Id, string Name, string Role);

    [Fact]
    public async Task Users_endpoint_lists_the_seeded_Claimants_and_Officers_without_identity_headers()
    {
        var client = factory.CreateClient();

        var users = await client.GetFromJsonAsync<List<UserDto>>("/api/users");

        Assert.NotNull(users);
        Assert.Equal(2, users.Count(u => u.Role == "Claimant"));
        Assert.Equal(3, users.Count(u => u.Role == "Officer"));
    }

    [Fact]
    public async Task Request_without_identity_headers_is_refused_with_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/test/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(OfficerId, "", "only one header")]
    [InlineData("not-a-guid", "Officer", "id is not a guid")]
    [InlineData("99999999-0000-0000-0000-000000000001", "Officer", "unknown user")]
    [InlineData(OfficerId, "Claimant", "role does not match the seeded user")]
    [InlineData(OfficerId, "1", "role is a number, not a role name")]
    public async Task Request_whose_identity_headers_do_not_match_a_seeded_user_is_refused_with_401(
        string userId, string role, string because)
    {
        var response = await GetWhoAmI(userId, role);

        _ = because; // names the case in the test runner output
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_to_an_unknown_route_without_identity_headers_is_refused_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/no-such-route");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_a_seeded_users_headers_is_served_as_that_user()
    {
        var response = await GetWhoAmI(OfficerId, "Officer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{OfficerId} Officer", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task Api_docs_are_served_without_identity_headers(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Browser_CORS_preflight_from_the_Angular_app_is_answered_without_identity_headers()
    {
        // A browser sends this OPTIONS request, without our headers, before any call that carries them.
        var request = new HttpRequestMessage(HttpMethod.Options, "/test/whoami");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "x-user-id,x-role");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:4200", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    // Priya Nair, a seeded Officer.
    private const string OfficerId = "22222222-0000-0000-0000-000000000001";

    private Task<HttpResponseMessage> GetWhoAmI(string userId, string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/whoami");
        request.Headers.Add("X-User-Id", userId);
        request.Headers.Add("X-Role", role);
        return factory.CreateClient().SendAsync(request);
    }
}
