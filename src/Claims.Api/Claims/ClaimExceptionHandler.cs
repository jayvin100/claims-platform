using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Claims;

/// <summary>
/// Turns a broken Claim rule into a problem-details response, so controllers don't catch exceptions
/// and the message the Claim wrote reaches the user unchanged.
/// </summary>
public class ClaimExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InvalidClaimException)
            return false; // anything else is a real fault: the default 500

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The Claim is not valid.",
                Detail = exception.Message,
            },
        });
    }
}
