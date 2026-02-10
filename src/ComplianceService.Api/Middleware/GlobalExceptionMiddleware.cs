using System.Net;
using System.Text.Json;

namespace ComplianceService.Api.Middleware;

/// <summary>
/// Global exception handler middleware
/// Catches unhandled exceptions and returns standardized error responses
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "An error occurred while processing your request",
            status = context.Response.StatusCode,
            detail = _environment.IsDevelopment() ? exception.Message : "An internal server error occurred",
            instance = context.Request.Path.ToString(),
            traceId = context.TraceIdentifier
        };

        // Include stack trace in development
        if (_environment.IsDevelopment())
        {
            var detailedProblem = new
            {
                problemDetails.type,
                problemDetails.title,
                problemDetails.status,
                problemDetails.detail,
                problemDetails.instance,
                problemDetails.traceId,
                stackTrace = exception.StackTrace,
                innerException = exception.InnerException?.Message
            };

            await context.Response.WriteAsJsonAsync(detailedProblem);
        }
        else
        {
            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
