using System.Security.Claims;

namespace Claims.Api.Infrastructure.Identity;

/// <summary>
/// Who is calling, for ownership and role checks. It reads the signed-in ASP.NET Core user, not the headers.
/// With real login (JWT, ADR-0006), Program.cs swaps the authentication scheme; this keeps reading the signed-in
/// user (it may need a claim mapping); controllers and authorisation rules stay; the Angular role picker and
/// identity interceptor become a login flow.
/// </summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor)
{
    private ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("No current HTTP request.");

    public Guid Id => Guid.Parse(Principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public UserRole Role => Enum.Parse<UserRole>(Principal.FindFirstValue(ClaimTypes.Role)!);
}
