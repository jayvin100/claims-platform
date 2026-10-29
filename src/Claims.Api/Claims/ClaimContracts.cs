using System.ComponentModel.DataAnnotations;

namespace Claims.Api.Claims;

// Request and response bodies for the Claims endpoints. Enums travel as their names ("SG", "Motor").

/// <summary>What a Claimant enters. There is no currency: it comes from the Market (D7).</summary>
public record SubmitClaimRequest(
    [Required, MaxLength(50)] string PolicyNumber,
    [Required] Market? Market,
    [Required] ClaimType? ClaimType,
    [Required] DateOnly? IncidentDate,
    [Required, MaxLength(2000)] string Description);

/// <summary>The Reserve amount only: its currency is the Claim's, from the Market (D7).</summary>
public record SetReserveRequest([Required] decimal? Amount);

/// <summary>The reason is shown to the Claimant on their timeline.</summary>
public record RejectClaimRequest([Required, MaxLength(1000)] string Reason);

/// <summary>One row of a Claimant's My Claims list.</summary>
public record ClaimSummaryResponse(Guid Id, string ClaimNumber, Market Market, ClaimType ClaimType, ClaimStatus Status, DateTimeOffset CreatedAt);

public record ClaimHistoryResponse(ClaimStatus Status, string Description, DateTimeOffset OccurredAt);

public record ClaimResponse(
    Guid Id, string ClaimNumber, string PolicyNumber, Market Market, string Currency, ClaimType ClaimType,
    DateOnly IncidentDate, string Description, ClaimStatus Status, Guid ClaimantId, Guid? AssignedOfficerId,
    decimal? ReserveAmount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, List<ClaimHistoryResponse> History)
{
    public static ClaimResponse From(Claim claim) => new(
        claim.Id, claim.ClaimNumber, claim.PolicyNumber, claim.Market, claim.Currency, claim.ClaimType,
        claim.IncidentDate, claim.Description, claim.Status, claim.ClaimantId, claim.AssignedOfficerId,
        claim.ReserveAmount, claim.CreatedAt, claim.UpdatedAt,
        claim.History.OrderBy(h => h.Id).Select(h => new ClaimHistoryResponse(h.Status, h.Description, h.OccurredAt)).ToList());
}

/// <summary>One row of an Officer's Queue. The UI shows age from CreatedAt.</summary>
public record QueueItemResponse(
    Guid Id, string ClaimNumber, Market Market, ClaimType ClaimType, ClaimStatus Status, DateTimeOffset CreatedAt, bool IsMine);
