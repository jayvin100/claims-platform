using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Claims;

[ApiController]
[Route("api/claims")]
public class ClaimsController(AppDbContext db, CurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Claimant))]
    [EndpointSummary("Submits a Claim and returns it with its Claim Number")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(SubmitClaimRequest request)
    {
        // [ApiController] has already answered 400 if a required field was missing, so .Value is safe.
        var claim = Claim.Submit(
            currentUser.Id, request.PolicyNumber, request.Market!.Value, request.ClaimType!.Value,
            request.IncidentDate!.Value, request.Description, await db.NextClaimSequenceAsync(), clock.GetUtcNow());

        db.Claims.Add(claim);
        await db.SaveChangesAsync();

        return Created($"/api/claims/{claim.Id}", ClaimResponse.From(claim));
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Claimant))]
    [EndpointSummary("Lists the caller's own Claims, newest first")]
    public async Task<List<ClaimSummaryResponse>> ListMine() =>
        await db.Claims
            .Where(c => c.ClaimantId == currentUser.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClaimSummaryResponse(c.Id, c.ClaimNumber, c.Market, c.ClaimType, c.Status, c.CreatedAt))
            .ToListAsync();

    [HttpGet("{id:guid}")]
    [EndpointSummary("A Claim with its Claim History: the owning Claimant or any Officer")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimResponse>> Get(Guid id)
    {
        var claim = await db.Claims.Include(c => c.History).SingleOrDefaultAsync(c => c.Id == id);

        // Another Claimant's Claim answers 404, not 403, so its existence isn't revealed (D23).
        bool canSee = currentUser.Role == UserRole.Officer || claim?.ClaimantId == currentUser.Id;
        if (claim is null || !canSee)
            return NotFound();

        return ClaimResponse.From(claim);
    }

    [HttpPost("{id:guid}/pick-up")]
    [Authorize(Roles = nameof(UserRole.Officer))]
    [EndpointSummary("Officer only: picks up a Submitted Claim, becoming its Assigned Officer")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClaimResponse>> PickUp(Guid id)
    {
        var claim = await db.Claims.Include(c => c.History).SingleOrDefaultAsync(c => c.Id == id);
        if (claim is null)
            return NotFound();

        // Two Officers at once: the later save fails its xmin check and ClaimExceptionHandler answers 409.
        claim.PickUp(currentUser.Id, clock.GetUtcNow());
        await db.SaveChangesAsync();

        return ClaimResponse.From(claim);
    }
}
