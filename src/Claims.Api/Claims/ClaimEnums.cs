namespace Claims.Api.Claims;

public enum ClaimType
{
    Motor,
    Property,
}

/// <summary>Where a Claim is in its lifecycle (D5). Settled and Rejected are final.</summary>
public enum ClaimStatus
{
    Submitted,
    UnderReview,
    InfoRequested,
    Approved,
    Settled,
    Rejected,
}
