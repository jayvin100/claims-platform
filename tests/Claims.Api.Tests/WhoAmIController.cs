using Claims.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Tests;

/// <summary>
/// A test-only endpoint with no special attributes, standing in for "any business endpoint"
/// until the Claims endpoints exist. ApiFactory registers it with the real API.
/// </summary>
[ApiController]
[Route("test/whoami")]
public class WhoAmIController(CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public string Get() => $"{currentUser.Id} {currentUser.Role}";
}
