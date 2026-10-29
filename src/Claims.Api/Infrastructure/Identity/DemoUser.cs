namespace Claims.Api.Infrastructure.Identity;

public enum UserRole
{
    Claimant,
    Officer,
}

/// <summary>A seeded person the role picker can act as. There is no login (ADR-0006).</summary>
public class DemoUser
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public UserRole Role { get; init; }

    // Fixed ids so the Angular app, tests and Swagger examples can rely on them.
    public static readonly DemoUser[] Seed =
    [
        new() { Id = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Aisha Rahman", Role = UserRole.Claimant },
        new() { Id = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "Daniel Tan", Role = UserRole.Claimant },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000001"), Name = "Priya Nair", Role = UserRole.Officer },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000002"), Name = "Marcus Lim", Role = UserRole.Officer },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000003"), Name = "Siti Hassan", Role = UserRole.Officer },
    ];
}
