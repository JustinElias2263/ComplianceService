using System.Diagnostics;

namespace ComplianceService.Api.Middleware;

/// <summary>
/// Request logging middleware
/// Logs HTTP request/response details with execution time
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;

        _logger.LogInformation(
            "HTTP {RequestMethod} {RequestPath} started",
            requestMethod,
            requestPath);

        try
        {
            await _next(context);

            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath} completed with status {StatusCode} in {ElapsedMs}ms",
                requestMethod,
                requestPath,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "HTTP {RequestMethod} {RequestPath} failed with exception after {ElapsedMs}ms",
                requestMethod,
                requestPath,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
