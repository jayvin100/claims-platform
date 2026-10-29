using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>
/// Below the HTTP API: proves the Claim's `xmin` concurrency token itself (D13), not just the status rule.
/// Both contexts load the Claim while it is still Submitted, so only the database can catch the clash.
/// </summary>
public class ClaimConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Second_of_two_saves_of_the_same_Claim_loaded_at_the_same_time_is_a_concurrency_conflict()
    {
        var claimId = await SaveSubmittedClaim();
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var claimA = await dbA.Claims.SingleAsync(c => c.Id == claimId);
        var claimB = await dbB.Claims.SingleAsync(c => c.Id == claimId);

        claimA.PickUp(Priya.Id, DateTimeOffset.UtcNow);
        claimB.PickUp(Marcus.Id, DateTimeOffset.UtcNow);
        await dbA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

        using var scopeCheck = factory.Services.CreateScope();
        var dbCheck = scopeCheck.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbCheck.Claims.SingleAsync(c => c.Id == claimId);
        Assert.Equal(Priya.Id, saved.AssignedOfficerId);
    }

    private async Task<Guid> SaveSubmittedClaim()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var claim = Claim.Submit(Aisha.Id, "POL-123", Market.SG, ClaimType.Motor,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), "Rear-ended at a junction",
            await db.NextClaimSequenceAsync(), DateTimeOffset.UtcNow);
        db.Claims.Add(claim);
        await db.SaveChangesAsync();
        return claim.Id;
    }
}
