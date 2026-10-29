using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Claims;

/// <summary>
/// Turns a broken Claim rule into a problem-details response, so controllers don't catch exceptions
/// and the message the Claim wrote reaches the user unchanged (D12).
/// </summary>
public class ClaimExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title, string detail)? problem = exception switch
        {
            InvalidClaimException => (StatusCodes.Status400BadRequest, "The Claim is not valid.", exception.Message),
            ClaimConflictException => (StatusCodes.Status409Conflict, "The Claim's status doesn't allow this.", exception.Message),
            NotAssignedOfficerException => (StatusCodes.Status403Forbidden, "This Claim is held by another Officer.", exception.Message),
            // The Claim's xmin no longer matched on save: another request changed it first (D13).
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The Claim was changed at the same time.",
                "Someone else changed this Claim a moment ago. Reload it and try again."),
            _ => null,
        };
        if (problem is null)
            return false; // anything else is a real fault: the default 500

        context.Response.StatusCode = problem.Value.status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = problem.Value.status,
                Title = problem.Value.title,
                Detail = problem.Value.detail,
            },
        });
    }
}
