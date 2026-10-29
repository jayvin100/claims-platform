namespace Claims.Api.Claims;

/// <summary>
/// The Claim's current status doesn't allow the action (for example, picking up a Claim that is
/// already Under Review). The API answers 409 with this message (D12).
/// </summary>
public class ClaimConflictException(string message) : Exception(message);
