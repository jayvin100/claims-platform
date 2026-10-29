namespace Claims.Api.Tests;

/// <summary>The seeded Demo Users the tests act as (D26), and a client that sends their identity headers.</summary>
public static class TestUsers
{
    public record DemoUser(Guid Id, string Role);

    public static readonly DemoUser Aisha = new(Guid.Parse("11111111-0000-0000-0000-000000000001"), "Claimant");
    public static readonly DemoUser Daniel = new(Guid.Parse("11111111-0000-0000-0000-000000000002"), "Claimant");
    public static readonly DemoUser Priya = new(Guid.Parse("22222222-0000-0000-0000-000000000001"), "Officer");
    public static readonly DemoUser Marcus = new(Guid.Parse("22222222-0000-0000-0000-000000000002"), "Officer");

    public static HttpClient ClientAs(this ApiFactory factory, DemoUser user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", user.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Role", user.Role);
        return client;
    }
}
