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
    // Two Officers at once: the later save fails its xmin check and ClaimExceptionHandler answers 409.
    public Task<ActionResult<ClaimResponse>> PickUp(Guid id) =>
        ActOnClaim(id, claim => claim.PickUp(currentUser.Id, clock.GetUtcNow()));

    [HttpPut("{id:guid}/reserve")]
    [Authorize(Roles = nameof(UserRole.Officer))]
    [EndpointSummary("Assigned Officer only: sets or revises the Reserve while the Claim is Under Review")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ClaimResponse>> SetReserve(Guid id, SetReserveRequest request) =>
        ActOnClaim(id, claim => claim.SetReserve(currentUser.Id, request.Amount!.Value, clock.GetUtcNow()));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = nameof(UserRole.Officer))]
    [EndpointSummary("Assigned Officer only: approves a Claim Under Review at its current Reserve")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ClaimResponse>> Approve(Guid id) =>
        ActOnClaim(id, claim => claim.Approve(currentUser.Id, clock.GetUtcNow()));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = nameof(UserRole.Officer))]
    [EndpointSummary("Assigned Officer only: rejects a Claim Under Review, with a reason the Claimant sees")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ClaimResponse>> Reject(Guid id, RejectClaimRequest request) =>
        ActOnClaim(id, claim => claim.Reject(currentUser.Id, request.Reason, clock.GetUtcNow()));

    [HttpPost("{id:guid}/settle")]
    [Authorize(Roles = nameof(UserRole.Officer))]
    [EndpointSummary("Assigned Officer only: settles an Approved Claim")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ClaimResponse>> Settle(Guid id) =>
        ActOnClaim(id, claim => claim.Settle(currentUser.Id, clock.GetUtcNow()));

    // Every Officer action: load the Claim, let it apply its own rule, save, and return the updated Claim.
    // A broken rule throws, and ClaimExceptionHandler turns it into 400, 403 or 409.
    private async Task<ActionResult<ClaimResponse>> ActOnClaim(Guid id, Action<Claim> action)
    {
        var claim = await db.Claims.Include(c => c.History).SingleOrDefaultAsync(c => c.Id == id);
        if (claim is null)
            return NotFound();

        action(claim);
        await db.SaveChangesAsync();

        return ClaimResponse.From(claim);
    }
}
