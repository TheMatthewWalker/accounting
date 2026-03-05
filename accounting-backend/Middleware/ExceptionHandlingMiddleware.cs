using System.Net;
using System.Text.Json;
using AccountingApp.Exceptions;
using AccountingApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccountingApp.Middleware;

/// <summary>
/// Global exception handling middleware
/// Catches all unhandled exceptions and maps them to standardized API responses
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred");
            await HandleExceptionAsync(context, exception);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var traceId = context.TraceIdentifier;
        var response = new ApiResponse<object>();

        switch (exception)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = validationEx.Code,
                        Message = validationEx.Message,
                        Errors = validationEx.ValidationErrors?
                            .SelectMany(kvp => kvp.Value.Select(v => new FieldError
                            {
                                Field = kvp.Key,
                                Message = v
                            }))
                            .ToList()
                    },
                    TraceId = traceId
                };
                break;

            case ResourceNotFoundException notFoundEx:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = notFoundEx.Code,
                        Message = notFoundEx.Message
                    },
                    TraceId = traceId
                };
                break;

            case UnauthorizedException unauthorizedEx:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = unauthorizedEx.Code,
                        Message = unauthorizedEx.Message
                    },
                    TraceId = traceId
                };
                break;

            case ForbiddenException forbiddenEx:
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = forbiddenEx.Code,
                        Message = forbiddenEx.Message
                    },
                    TraceId = traceId
                };
                break;

            case DuplicateResourceException duplicateEx:
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = duplicateEx.Code,
                        Message = duplicateEx.Message
                    },
                    TraceId = traceId
                };
                break;

            case BusinessRuleException businessEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = businessEx.Code,
                        Message = businessEx.Message
                    },
                    TraceId = traceId
                };
                break;

            case OperationFailedException operationEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = operationEx.Code,
                        Message = operationEx.Message
                    },
                    TraceId = traceId
                };
                break;

            case ServiceUnavailableException serviceEx:
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = serviceEx.Code,
                        Message = serviceEx.Message
                    },
                    TraceId = traceId
                };
                break;

            // Handle ModelState validation errors (from ModelStateDictionary)
            case InvalidOperationException when exception.Message.Contains("ModelState"):
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = "VALIDATION_ERROR",
                        Message = "One or more validation errors occurred.",
                        Details = exception.Message
                    },
                    TraceId = traceId
                };
                break;

            // Default: Internal server error
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                var isDevelopment = context.RequestServices
                    .GetRequiredService<IHostEnvironment>()
                    .IsDevelopment();

                response = new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = "INTERNAL_SERVER_ERROR",
                        Message = "An internal server error occurred. Please try again later.",
                        Details = isDevelopment ? exception.ToString() : null
                    },
                    TraceId = traceId
                };
                break;
        }

        return context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Extension method to register exception handling middleware
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
