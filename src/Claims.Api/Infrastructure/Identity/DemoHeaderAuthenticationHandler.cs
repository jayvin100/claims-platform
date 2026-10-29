using System.Security.Claims;
using System.Text.Encodings.Web;
using Claims.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Claims.Api.Infrastructure.Identity;

/// <summary>
/// The only code that reads X-User-Id and X-Role (ADR-0006). It turns valid headers into a normal
/// ASP.NET Core user, so swapping to real login means registering AddJwtBearer instead of this
/// scheme in Program.cs; controllers and CurrentUser stay the same.
/// </summary>
public class DemoHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoHeaders";
    public const string UserIdHeader = "X-User-Id";
    public const string RoleHeader = "X-Role";
    public static readonly string[] Headers = [UserIdHeader, RoleHeader];

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string userIdValue = Request.Headers[UserIdHeader].ToString();
        string roleValue = Request.Headers[RoleHeader].ToString();

        // No headers at all: "not signed in". The authorization policy then answers 401.
        if (userIdValue == "" && roleValue == "")
            return AuthenticateResult.NoResult();

        // Enum.TryParse alone would also accept "1", so check the name exactly.
        if (!Guid.TryParse(userIdValue, out var userId) || !Enum.GetNames<UserRole>().Contains(roleValue))
            return AuthenticateResult.Fail("X-User-Id must be a user id and X-Role must be Claimant or Officer.");

        var role = Enum.Parse<UserRole>(roleValue);
        bool isSeededUser = await db.Users.AnyAsync(u => u.Id == userId && u.Role == role);
        if (!isSeededUser)
            return AuthenticateResult.Fail("X-User-Id and X-Role do not match a demo user.");

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role.ToString())],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
