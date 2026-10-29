using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Reporting;

/// <summary>
/// Read from Reporting's own table, which Kafka feeds, so the figures lag the Claims by a few seconds (ADR-0003).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = nameof(UserRole.Officer))]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("exposure")]
    [EndpointSummary("Officer only: per Market, open Claims, Open Reserves in that Market's currency, and Unreserved Claims")]
    public Task<List<MarketExposureResponse>> Exposure() => ReportingQueries.Exposure(db);

    [HttpGet("workload")]
    [EndpointSummary("Officer only: each Officer's Claims by status, plus unassigned Submitted Claims")]
    public Task<WorkloadResponse> Workload() => ReportingQueries.Workload(db);
}
