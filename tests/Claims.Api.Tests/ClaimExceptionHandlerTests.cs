using System.Text.Json;
using Claims.Api.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.Tests;

/// <summary>
/// The handler on its own, with a bare HttpContext: an xmin clash can't be forced through the HTTP API on cue,
/// so this proves the last step, exception to 409 (D13). ClaimConcurrencyTests proves the clash itself.
/// </summary>
public class ClaimExceptionHandlerTests
{
    [Fact]
    public async Task A_concurrency_conflict_on_save_becomes_a_409_with_problem_details()
    {
        // The same problem-details setup as Program.cs; the JSON writer reads its options from RequestServices.
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var handler = new ClaimExceptionHandler(services.GetRequiredService<IProblemDetailsService>());

        var handled = await handler.TryHandleAsync(context, new DbUpdateConcurrencyException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("The Claim was changed at the same time.", body.RootElement.GetProperty("title").GetString());
    }
}
