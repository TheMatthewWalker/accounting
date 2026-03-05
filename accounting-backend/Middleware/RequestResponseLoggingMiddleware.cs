using System.Diagnostics;

namespace AccountingApp.Middleware;

/// <summary>
/// Middleware for logging all HTTP requests and responses
/// Provides comprehensive audit trail of API activity
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;
        var requestPath = request.Path;
        var requestMethod = request.Method;

        _logger.LogInformation(
            "HTTP {Method} {Path} started - TraceId: {TraceId}",
            requestMethod,
            requestPath,
            context.TraceIdentifier);

        // Log sensitive path info without capturing body
        if (request.ContentLength.HasValue && request.ContentLength > 0)
        {
            LogRequestDetails(requestMethod, requestPath);
        }

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "HTTP {Method} {Path} completed with status {StatusCode} in {ElapsedMilliseconds}ms - TraceId: {TraceId}",
                requestMethod,
                requestPath,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.TraceIdentifier);
        }
    }

    private void LogRequestDetails(string method, string path)
    {
        if (path.Contains("register", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("login", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Sensitive request received for {Path}", path);
        }
        else
        {
            _logger.LogDebug("Request body present for {Method} {Path}", method, path);
        }
    }
}

/// <summary>
/// Extension method to register request/response logging middleware
/// </summary>
public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}
