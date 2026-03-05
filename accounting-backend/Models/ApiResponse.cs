namespace AccountingApp.Models;

/// <summary>
/// Standard API response envelope for all responses (success and error)
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates whether the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The actual response data (present only for successful responses)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Error information (present only for error responses)
    /// </summary>
    public ApiError? Error { get; set; }

    /// <summary>
    /// Unique identifier for tracking this request through logs
    /// Useful for debugging and support inquiries
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Creates a successful response
    /// </summary>
    public static ApiResponse<T> SuccessResponse(T data, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Error = null,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates an error response
    /// </summary>
    public static ApiResponse<T> ErrorResponse(ApiError error, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Error = error,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates an error response from exception details
    /// </summary>
    public static ApiResponse<T> ErrorResponse(string code, string message, string? details = null, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details
            },
            TraceId = traceId
        };
    }
}

/// <summary>
/// Non-generic version of ApiResponse for endpoints that don't return typed data
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    /// <summary>
    /// Creates a successful response without data
    /// </summary>
    public static new ApiResponse SuccessResponse(string? traceId = null)
    {
        return new ApiResponse
        {
            Success = true,
            Data = null,
            Error = null,
            TraceId = traceId
        };
    }
}

/// <summary>
/// Error details in the API response
/// </summary>
public class ApiError
{
    /// <summary>
    /// Machine-readable error code (e.g., "VALIDATION_ERROR", "NOT_FOUND", "UNAUTHORIZED")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message suitable for displaying to users
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed information about validation errors (field-level errors)
    /// Only populated for validation failures
    /// </summary>
    public List<FieldError>? Errors { get; set; }

    /// <summary>
    /// Additional technical details for debugging
    /// Only shown to developers in development environments
    /// Excluded from production responses for security
    /// </summary>
    public string? Details { get; set; }

    public ApiError()
    {
    }

    public ApiError(string code, string message, string? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }
}

/// <summary>
/// Field-level validation error
/// </summary>
public class FieldError
{
    /// <summary>
    /// The name of the field that failed validation
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The validation error message for this field
    /// </summary>
    public string Message { get; set; } = string.Empty;

    public FieldError()
    {
    }

    public FieldError(string field, string message)
    {
        Field = field;
        Message = message;
    }
}
