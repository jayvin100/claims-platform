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

public record ReserveSet(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt,
    decimal Amount, string Currency)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);

public record InfoRequested(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt,
    Guid ClaimantId, string Question)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);

// Only the common fields: Reporting learns the status change, and Notifications ignores it.
public record InfoProvided(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);

public record ClaimApproved(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt,
    Guid ClaimantId)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);

public record ClaimRejected(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt,
    Guid ClaimantId, string Reason)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);

public record ClaimSettled(
    Guid EventId, Guid ClaimId, string ClaimNumber, Market Market, DateTimeOffset OccurredAt,
    Guid ClaimantId)
    : ClaimEvent(EventId, ClaimId, ClaimNumber, Market, OccurredAt);
