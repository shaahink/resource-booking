using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Services;

/// <summary>
/// Maps domain exceptions to the contract's ProblemDetails shapes (docs/contracts.md § Error shape):
/// BookingRejectedException → 409/422 with the `violations` extension,
/// KeyNotFoundException → 404, UnauthorizedAccessException → 403.
/// </summary>
public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            BookingRejectedException rejected => WithViolations(new ProblemDetails
            {
                Title = "Booking rejected",
                Status = rejected.IsConflict ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity,
            }, rejected),
            KeyNotFoundException notFound => new ProblemDetails
            {
                Title = notFound.Message,
                Status = StatusCodes.Status404NotFound,
            },
            UnauthorizedAccessException denied => new ProblemDetails
            {
                Title = denied.Message,
                Status = StatusCodes.Status403Forbidden,
            },
            _ => null,
        };

        if (problem is null) return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails WithViolations(ProblemDetails problem, BookingRejectedException rejected)
    {
        problem.Extensions["violations"] = rejected.Violations;
        return problem;
    }
}
