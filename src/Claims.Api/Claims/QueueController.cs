using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Claims;

[ApiController]
[Route("api/queue")]
[Authorize(Roles = nameof(UserRole.Officer))]
public class QueueController(AppDbContext db, CurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Read live from the Claims tables, not the Reporting read model, so an Officer never sees a Claim
    /// as free after someone else took it (D18).
    /// </summary>
    [HttpGet]
    [EndpointSummary("Officer only: unassigned Submitted Claims plus the caller's open Claims, oldest first")]
    public async Task<List<QueueItemResponse>> Get()
    {
        var officerId = currentUser.Id;
        return await db.Claims
            // A Submitted Claim has no Assigned Officer yet; Settled and Rejected are final, so no longer work.
            .Where(c => c.Status == ClaimStatus.Submitted
                || (c.AssignedOfficerId == officerId && c.Status != ClaimStatus.Settled && c.Status != ClaimStatus.Rejected))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.ClaimNumber) // unique, so Claims created at the same instant don't swap between polls
            .Select(c => new QueueItemResponse(
                c.Id, c.ClaimNumber, c.Market, c.ClaimType, c.Status, c.CreatedAt, c.AssignedOfficerId == officerId))
            .ToListAsync();
    }
}
