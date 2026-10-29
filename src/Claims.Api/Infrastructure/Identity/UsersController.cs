using Claims.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Infrastructure.Identity;

public record UserResponse(Guid Id, string Name, string Role);

[ApiController]
[Route("api/users")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous] // The role picker calls this before anyone is chosen.
    [EndpointSummary("Demo only: lists the seeded users for the role picker")]
    [EndpointDescription("There is no login. Pick a user here and send their id and role as X-User-Id and X-Role on every other request.")]
    public async Task<List<UserResponse>> List() =>
        await db.Users
            .OrderBy(u => u.Role).ThenBy(u => u.Name)
            .Select(u => new UserResponse(u.Id, u.Name, u.Role.ToString()))
            .ToListAsync();
}
