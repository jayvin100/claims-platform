using System.Security.Claims;

namespace Claims.Api.Infrastructure.Identity;

/// <summary>
/// Who is calling, for ownership and role checks. It reads the signed-in ASP.NET Core user,
/// not the headers, so it works unchanged whether that user came from demo headers or a JWT.
/// </summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor)
{
    private ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("No current HTTP request.");

    public Guid Id => Guid.Parse(Principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public UserRole Role => Enum.Parse<UserRole>(Principal.FindFirstValue(ClaimTypes.Role)!);
}
