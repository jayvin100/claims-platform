using Claims.Api.Claims;

namespace Claims.Api.Reporting;

// Response bodies for the report endpoints. Enums travel as their names ("SG").

/// <summary>
/// One Market's Exposure (D17). Open means neither Settled nor Rejected. OpenReserves is in this Market's own
/// currency and is never added to another Market's; UnreservedClaims are open Claims nobody has priced yet.
/// </summary>
public record MarketExposureResponse(Market Market, string Currency, int OpenClaims, decimal OpenReserves, int UnreservedClaims);

/// <summary>One Officer's Claims: the open ones by status, then the Settled and Rejected counts.</summary>
public record OfficerWorkloadResponse(Guid OfficerId, int UnderReview, int InfoRequested, int Approved, int Settled, int Rejected);

/// <summary>The Team's Workload: Submitted Claims nobody has picked up yet, then each Officer's Claims.</summary>
public record WorkloadResponse(int UnassignedSubmitted, List<OfficerWorkloadResponse> Officers);
