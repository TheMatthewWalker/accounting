using System.ComponentModel.DataAnnotations;

namespace AccountingApp.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(string email, string password, string firstName, string lastName);
    Task<AuthResponse> LoginAsync(string email, string password);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    string GenerateJwtToken(Guid userId, string email);
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public Guid? UserId { get; set; }
}

public interface IGLAccountService
{
    Task<GLAccountResponse> CreateAccountAsync(Guid organisationId, CreateGLAccountRequest request);
    Task<GLAccountResponse> GetAccountAsync(Guid accountId);
    Task<IEnumerable<GLAccountResponse>> GetAccountsByOrganisationAsync(Guid organisationId);
    Task<GLAccountResponse> UpdateAccountAsync(Guid accountId, UpdateGLAccountRequest request);
    Task DeleteAccountAsync(Guid accountId);
}

public class CreateGLAccountRequest
{
    [Required]
    [StringLength(10, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required]
    [RegularExpression("^(Asset|Liability|Equity|Revenue|Expense)$")]
    public string Type { get; set; } = string.Empty;
    public string? SubType { get; set; }
    public decimal OpeningBalance { get; set; }
}

public class UpdateGLAccountRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class GLAccountResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? SubType { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
}

public interface IDaybookService
{
    Task<DaybookResponse> CreateDaybookEntryAsync(Guid organisationId, CreateDaybookRequest request);
    Task<DaybookResponse> GetDaybookEntryAsync(Guid entryId);
    Task<IEnumerable<DaybookResponse>> GetDaybookEntriesByOrganisationAsync(Guid organisationId, DateTime? fromDate = null, DateTime? toDate = null);
    Task PostDaybookEntryAsync(Guid entryId);
    Task DeleteDaybookEntryAsync(Guid entryId);
}

public class CreateDaybookRequest
{
    [Required]
    [RegularExpression("^(Sales|Purchase|Journal|Bank|Receipt)$")]
    public string Type { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    [Required]
    [MinLength(2, ErrorMessage = "At least 2 journal lines are required")]
    public List<CreateJournalLineRequest> Lines { get; set; } = new List<CreateJournalLineRequest>();
}

public class CreateJournalLineRequest
{
    public Guid GLAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? NarrationLine { get; set; }
}

public class DaybookResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public bool IsPosted { get; set; }
    public List<JournalLineResponse> Lines { get; set; } = new List<JournalLineResponse>();
}

public class JournalLineResponse
{
    public Guid Id { get; set; }
    public Guid GLAccountId { get; set; }
    public string GLAccountName { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? NarrationLine { get; set; }
}

public interface IReportService
{
    Task<TrialBalanceResponse> GetTrialBalanceAsync(Guid organisationId, DateTime asOfDate);
    Task<IEnumerable<TAccountResponse>> GetTAccountsAsync(Guid organisationId, DateTime asOfDate);
    Task<TAccountResponse> GetTAccountAsync(Guid accountId, DateTime asOfDate);
    Task<GeneralLedgerResponse> GetGeneralLedgerAsync(Guid organisationId, DateTime fromDate, DateTime toDate);
}

public class TrialBalanceResponse
{
    public DateTime AsOfDate { get; set; }
    public List<TrialBalanceLineResponse> Lines { get; set; } = new List<TrialBalanceLineResponse>();
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
}

public class TrialBalanceLineResponse
{
    public Guid AccountId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class TAccountResponse
{
    public Guid AccountId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public List<TAccountLineResponse> Entries { get; set; } = new List<TAccountLineResponse>();
    public decimal ClosingBalance { get; set; }
}

public class TAccountLineResponse
{
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
}

public class GeneralLedgerResponse
{
    public List<GLEntryResponse> Entries { get; set; } = new List<GLEntryResponse>();
}

public class GLEntryResponse
{
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public interface ICustomerSupplierService
{
    Task<CustomerResponse> CreateCustomerAsync(Guid organisationId, CreateCustomerRequest request);
    Task<CustomerResponse> GetCustomerAsync(Guid customerId);
    Task<IEnumerable<CustomerResponse>> GetCustomersByOrganisationAsync(Guid organisationId);
    Task<CustomerResponse> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request);
    Task DeleteCustomerAsync(Guid customerId);

    Task<SupplierResponse> CreateSupplierAsync(Guid organisationId, CreateSupplierRequest request);
    Task<SupplierResponse> GetSupplierAsync(Guid supplierId);
    Task<IEnumerable<SupplierResponse>> GetSuppliersByOrganisationAsync(Guid organisationId);
    Task<SupplierResponse> UpdateSupplierAsync(Guid supplierId, UpdateSupplierRequest request);
    Task DeleteSupplierAsync(Guid supplierId);
}

public class CreateCustomerRequest
{
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
    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }
}

public class UpdateCustomerRequest
{
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
    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; }
}

public class CustomerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSupplierRequest
{
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
    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }
}

public class UpdateSupplierRequest
{
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
    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; }
}

public class SupplierResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}
