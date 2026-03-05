using System.ComponentModel.DataAnnotations;

namespace AccountingApp.Validators;

/// <summary>
/// Validates that a date is not in the future
/// </summary>
public class NotFutureDateAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is DateTime dateValue)
        {
            if (dateValue > DateTime.UtcNow)
            {
                return new ValidationResult(ErrorMessage ?? "The date cannot be in the future.");
            }
        }
        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a password has sufficient complexity
/// Requires: minimum 8 characters, at least one uppercase, one lowercase, one digit, one special character
/// </summary>
public class PasswordStrengthAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string password)
        {
            if (string.IsNullOrEmpty(password))
                return new ValidationResult("Password is required.");

            if (password.Length < 8)
                return new ValidationResult("Password must be at least 8 characters long.");

            bool hasUppercase = password.Any(char.IsUpper);
            bool hasLowercase = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));

            if (!hasUppercase)
                return new ValidationResult("Password must contain at least one uppercase letter.");
            if (!hasLowercase)
                return new ValidationResult("Password must contain at least one lowercase letter.");
            if (!hasDigit)
                return new ValidationResult("Password must contain at least one digit.");
            if (!hasSpecialChar)
                return new ValidationResult("Password must contain at least one special character.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a value is a valid GL Account Type
/// </summary>
public class ValidAccountTypeAttribute : ValidationAttribute
{
    private static readonly string[] ValidTypes = { "Asset", "Liability", "Equity", "Revenue", "Expense" };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string accountType)
        {
            if (!ValidTypes.Contains(accountType))
            {
                return new ValidationResult(
                    ErrorMessage ?? $"Account type must be one of: {string.Join(", ", ValidTypes)}");
            }
        }
        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a value is a valid Daybook Entry Type
/// </summary>
public class ValidEntryTypeAttribute : ValidationAttribute
{
    private static readonly string[] ValidTypes = { "Sales", "Purchase", "Journal", "Bank", "Receipt" };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string entryType)
        {
            if (!ValidTypes.Contains(entryType))
            {
                return new ValidationResult(
                    ErrorMessage ?? $"Entry type must be one of: {string.Join(", ", ValidTypes)}");
            }
        }
        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a value is a valid Subscription Tier
/// </summary>
public class ValidSubscriptionTierAttribute : ValidationAttribute
{
    private static readonly string[] ValidTiers = { "Free", "Pro", "Enterprise" };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string tier)
        {
            if (!ValidTiers.Contains(tier))
            {
                return new ValidationResult(
                    ErrorMessage ?? $"Subscription tier must be one of: {string.Join(", ", ValidTiers)}");
            }
        }
        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a value is a valid Organization Member Role
/// </summary>
public class ValidRoleAttribute : ValidationAttribute
{
    private static readonly string[] ValidRoles = { "Owner", "Admin", "Member" };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string role)
        {
            if (!ValidRoles.Contains(role))
            {
                return new ValidationResult(
                    ErrorMessage ?? $"Role must be one of: {string.Join(", ", ValidRoles)}");
            }
        }
        return ValidationResult.Success;
    }
}
