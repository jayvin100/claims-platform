namespace Claims.Api.Claims;

/// <summary>
/// A domain event: something that happened to a Claim. Every event carries these common fields (D32);
/// each subtype adds what its consumers need. Notifications and Reporting only ever see events,
/// never the Claims tables (ADR-0001).
/// </summary>
public abstract record ClaimEvent(Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt);

public record ClaimSubmitted(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt,
    Guid ClaimantId, string Currency)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);

public record ClaimPickedUp(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt,
    Guid AssignedOfficerId, Guid ClaimantId)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);
