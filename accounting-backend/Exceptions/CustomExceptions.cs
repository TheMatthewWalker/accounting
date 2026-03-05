namespace AccountingApp.Exceptions;

/// <summary>
/// Thrown when input validation fails
/// Maps to 400 Bad Request
/// </summary>
public class ValidationException : Exception
{
    public string Code { get; set; } = "VALIDATION_ERROR";
    public Dictionary<string, string[]>? ValidationErrors { get; set; }

    public ValidationException(string message) : base(message)
    {
    }

    public ValidationException(string message, Dictionary<string, string[]> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors;
    }
}

/// <summary>
/// Thrown when a requested resource is not found
/// Maps to 404 Not Found
/// </summary>
public class ResourceNotFoundException : Exception
{
    public string Code { get; set; } = "NOT_FOUND";
    public string ResourceType { get; set; }
    public string ResourceId { get; set; }

    public ResourceNotFoundException(string resourceType, string resourceId)
        : base($"The {resourceType} with ID '{resourceId}' was not found.")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    public ResourceNotFoundException(string message) : base(message)
    {
        ResourceType = "Resource";
        ResourceId = string.Empty;
    }
}

/// <summary>
/// Thrown when authentication fails (invalid credentials, missing token, expired token)
/// Maps to 401 Unauthorized
/// </summary>
public class UnauthorizedException : Exception
{
    public string Code { get; set; } = "UNAUTHORIZED";

    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException() : base("Authentication required. Please provide valid credentials.")
    {
    }
}

/// <summary>
/// Thrown when user lacks permission to access a resource (authorization failure)
/// Maps to 403 Forbidden
/// </summary>
public class ForbiddenException : Exception
{
    public string Code { get; set; } = "FORBIDDEN";
    public string? Resource { get; set; }

    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, string resource) : base(message)
    {
        Resource = resource;
    }

    public ForbiddenException() : base("You do not have permission to access this resource.")
    {
    }
}

/// <summary>
/// Thrown when attempting to create a duplicate resource
/// Maps to 409 Conflict
/// </summary>
public class DuplicateResourceException : Exception
{
    public string Code { get; set; } = "DUPLICATE_RESOURCE";
    public string ResourceType { get; set; }
    public string? DuplicateField { get; set; }
    public string? DuplicateValue { get; set; }

    public DuplicateResourceException(string resourceType, string duplicateField, string duplicateValue)
        : base($"A {resourceType} with {duplicateField} '{duplicateValue}' already exists.")
    {
        ResourceType = resourceType;
        DuplicateField = duplicateField;
        DuplicateValue = duplicateValue;
    }

    public DuplicateResourceException(string message) : base(message)
    {
        ResourceType = "Resource";
    }
}

/// <summary>
/// Thrown when a business rule is violated (e.g., unbalanced journal entries, posting restrictions)
/// Maps to 400 Bad Request (or 422 Unprocessable Entity depending on implementation)
/// </summary>
public class BusinessRuleException : Exception
{
    public string Code { get; set; } = "BUSINESS_RULE_VIOLATION";
    public string? BusinessRule { get; set; }

    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string message, string businessRule) : base(message)
    {
        BusinessRule = businessRule;
    }
}

/// <summary>
/// Thrown when an operation fails for a recoverable reason
/// Maps to 400 Bad Request
/// </summary>
public class OperationFailedException : Exception
{
    public string Code { get; set; } = "OPERATION_FAILED";
    public string? Operation { get; set; }
    public string? Reason { get; set; }

    public OperationFailedException(string message) : base(message)
    {
    }

    public OperationFailedException(string message, string operation, string reason) : base(message)
    {
        Operation = operation;
        Reason = reason;
    }
}

/// <summary>
/// Thrown when a required dependency or service is unavailable
/// Maps to 503 Service Unavailable
/// </summary>
public class ServiceUnavailableException : Exception
{
    public string Code { get; set; } = "SERVICE_UNAVAILABLE";
    public string? ServiceName { get; set; }

    public ServiceUnavailableException(string serviceName)
        : base($"The {serviceName} service is currently unavailable. Please try again later.")
    {
        ServiceName = serviceName;
    }
}
