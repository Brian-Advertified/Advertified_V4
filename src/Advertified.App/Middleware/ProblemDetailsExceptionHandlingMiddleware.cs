using Advertified.App.Support;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Middleware;

public sealed class ProblemDetailsExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionHandlingMiddleware> _logger;

    public ProblemDetailsExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ProblemDetailsExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (TryMap(ex, out var problem))
        {
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}.", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Unexpected server error.",
                Detail = "The server could not complete this request.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    private bool TryMap(Exception exception, out ProblemDetails problem)
    {
        problem = new ProblemDetails();

        // Check for typed HTTP exceptions first
        if (exception is UnauthorizedException unauthorized)
        {
            problem = BuildProblem(StatusCodes.Status401Unauthorized, "Authentication required.", unauthorized.Message);
            return true;
        }

        if (exception is ForbiddenException forbidden)
        {
            problem = BuildProblem(StatusCodes.Status403Forbidden, "Forbidden.", forbidden.Message);
            return true;
        }

        if (exception is NotFoundException notFound)
        {
            problem = BuildProblem(StatusCodes.Status404NotFound, "Resource not found.", notFound.Message);
            return true;
        }

        // Legacy string-based matching for backward compatibility
        if (exception is InvalidOperationException invalidOperation)
        {
            var message = invalidOperation.Message;
            if (message.Contains("authenticated session", StringComparison.OrdinalIgnoreCase)
                || message.Contains("authenticated user account", StringComparison.OrdinalIgnoreCase))
            {
                problem = BuildProblem(StatusCodes.Status401Unauthorized, "Authentication required.", message);
                return true;
            }

            if (message.Contains("access is required", StringComparison.OrdinalIgnoreCase))
            {
                problem = BuildProblem(StatusCodes.Status403Forbidden, "Forbidden.", message);
                return true;
            }

            if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                problem = BuildProblem(StatusCodes.Status404NotFound, "Resource not found.", message);
                return true;
            }

            problem = BuildProblem(StatusCodes.Status400BadRequest, "Request could not be completed.", message);
            return true;
        }

        if (exception is KeyNotFoundException keyNotFound)
        {
            problem = BuildProblem(StatusCodes.Status404NotFound, "Resource not found.", keyNotFound.Message);
            return true;
        }

        return false;
    }

    private static ProblemDetails BuildProblem(int status, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
