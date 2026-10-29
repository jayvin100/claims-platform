namespace Claims.Api.Claims;

/// <summary>
/// The Claimant's input breaks a Claim rule (for example, a future incident date). The API answers 400.
/// A state rule, such as acting on a Settled Claim, is a different error that answers 409 (D12).
/// </summary>
public class InvalidClaimException(string message) : Exception(message);
