using System.ComponentModel.DataAnnotations;

namespace AccountingApp.Models;

/// <summary>
/// Represents a user in the system
/// </summary>
public class User
{
    public Guid Id { get; set; }
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; } = true;

    // OAuth2 fields
    public string? GoogleId { get; set; }
    public string? MicrosoftId { get; set; }

    // Navigation properties
    public ICollection<OrganisationMember> OrganisationMemberships { get; set; } = new List<OrganisationMember>();
}

/// <summary>
/// Represents an organisation (business/company)
/// </summary>
public class Organisation
{
    public Guid Id { get; set; }
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    [Required]
    [RegularExpression("^(Free|Pro|Enterprise)$")]
    public string SubscriptionTier { get; set; } = "Free"; // Free, Pro, Enterprise
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubscriptionExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    // VAT settings
    public Guid? DefaultVatAccountId { get; set; }
    public decimal VatReducedRate { get; set; } = 5m;
    public decimal VatFullRate { get; set; } = 20m;

    // Navigation properties
    public GLAccount? DefaultVatAccount { get; set; }
    public ICollection<OrganisationMember> Members { get; set; } = new List<OrganisationMember>();
    public ICollection<GLAccount> GLAccounts { get; set; } = new List<GLAccount>();
    public ICollection<DaybookEntry> DaybookEntries { get; set; } = new List<DaybookEntry>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
}

/// <summary>
/// Represents a user's membership in an organisation
/// </summary>
public class OrganisationMember
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid OrganisationId { get; set; }
    [Required]
    [RegularExpression("^(Viewer|Bookkeeper|Manager|Owner)$")]
    public string Role { get; set; } = "Viewer"; // Viewer, Bookkeeper, Manager, Owner
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User? User { get; set; }
    public Organisation? Organisation { get; set; }
}

/// <summary>
/// Represents a General Ledger account
/// </summary>
public class GLAccount
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    [Required]
    [StringLength(10, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required]
    [RegularExpression("^(Asset|Liability|Equity|Revenue|Expense)$")]
    public string Type { get; set; } = string.Empty; // Asset, Liability, Equity, Revenue, Expense
    public string? SubType { get; set; } // Current Asset, Fixed Asset, etc.
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Organisation? Organisation { get; set; }
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
}

/// <summary>
/// Represents a daybook entry (sales, purchases, journals)
/// </summary>
public class DaybookEntry
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    [Required]
    [RegularExpression("^(Sales|SalesReturn|Purchase|PurchaseReturn|Journal|Bank|Receipt|Payment)$")]
    public string Type { get; set; } = string.Empty; // Sales, SalesReturn, Purchase, PurchaseReturn, Journal, Receipt, Payment
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public bool IsPosted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }

    // Optional links to customer/supplier for structured daybook types
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }

    // Navigation properties
    public Organisation? Organisation { get; set; }
    public Customer? Customer { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}

/// <summary>
/// Represents a single journal entry (debit or credit line)
/// </summary>
public class JournalEntry
{
    public Guid Id { get; set; }
    public Guid DaybookEntryId { get; set; }
    public Guid GLAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? NarrationLine { get; set; }

    // Navigation properties
    public DaybookEntry? DaybookEntry { get; set; }
    public GLAccount? GLAccount { get; set; }
}

/// <summary>
/// Represents a customer
/// </summary>
public class Customer
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    [EmailAddress]
    public string? Email { get; set; }
    [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Phone number must be a valid format")]
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? ControlAccountId { get; set; } // Subsidiary account for AR (Accounts Receivable)
    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Organisation? Organisation { get; set; }
    public GLAccount? ControlAccount { get; set; }
}

/// <summary>
/// Represents a supplier
/// </summary>
public class Supplier
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    [EmailAddress]
    public string? Email { get; set; }
    [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Phone number must be a valid format")]
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? ControlAccountId { get; set; } // Subsidiary account for AP (Accounts Payable)
    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Organisation? Organisation { get; set; }
    public GLAccount? ControlAccount { get; set; }
}

/// <summary>
/// Represents a pending invitation for a user to join an organisation
/// </summary>
public class OrganisationInvitation
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    [Required]
    [EmailAddress]
    public string InvitedEmail { get; set; } = string.Empty;
    [Required]
    [RegularExpression("^(Viewer|Bookkeeper|Manager|Owner)$")]
    public string Role { get; set; } = "Viewer";
    [Required]
    public string Token { get; set; } = string.Empty; // shared with the invitee
    public Guid InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; } = false;

    // Navigation properties
    public Organisation? Organisation { get; set; }
    public User? InvitedByUser { get; set; }
}

/// <summary>
/// Represents an account balance at a specific date (for reporting)
/// </summary>
public class AccountBalance
{
    public Guid Id { get; set; }
    public Guid GLAccountId { get; set; }
    public DateTime BalanceDate { get; set; }
    public decimal Balance { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    // Navigation properties
    public GLAccount? GLAccount { get; set; }
}
