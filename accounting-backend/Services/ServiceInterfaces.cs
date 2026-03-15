using System.ComponentModel.DataAnnotations;

namespace AccountingApp.Services;

// ---- Organisation membership & invitation management ----

public interface IOrganisationService
{
    Task DeleteOrganisationAsync(Guid organisationId);
    Task<IEnumerable<MemberResponse>> GetMembersAsync(Guid organisationId);
    Task<MemberResponse> UpdateMemberRoleAsync(Guid organisationId, Guid memberId, UpdateMemberRoleRequest request);
    Task RemoveMemberAsync(Guid organisationId, Guid memberId, Guid requestingUserId);
    Task<InvitationResponse> CreateInvitationAsync(Guid organisationId, Guid invitedByUserId, CreateInvitationRequest request);
    Task<IEnumerable<InvitationResponse>> GetInvitationsAsync(Guid organisationId);
    Task CancelInvitationAsync(Guid organisationId, Guid invitationId);
    Task<MemberResponse> AcceptInvitationAsync(string token, Guid acceptingUserId);
}

public class MemberResponse
{
    public Guid MemberId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class UpdateMemberRoleRequest
{
    [Required]
    [RegularExpression("^(Viewer|Bookkeeper|Manager|Owner)$")]
    public string Role { get; set; } = string.Empty;
}

public class CreateInvitationRequest
{
    [Required]
    [EmailAddress]
    public string InvitedEmail { get; set; } = string.Empty;
    [Required]
    [RegularExpression("^(Viewer|Bookkeeper|Manager|Owner)$")]
    public string Role { get; set; } = string.Empty;
}

public class InvitationResponse
{
    public Guid Id { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
}

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
    Task<DaybookResponse> CreateSalesDaybookAsync(Guid organisationId, CreateSalesDaybookRequest request);
    Task<DaybookResponse> CreateSalesReturnDaybookAsync(Guid organisationId, CreateSalesReturnDaybookRequest request);
    Task<DaybookResponse> CreatePurchaseDaybookAsync(Guid organisationId, CreatePurchaseDaybookRequest request);
    Task<DaybookResponse> CreatePurchaseReturnDaybookAsync(Guid organisationId, CreatePurchaseDaybookRequest request);
    Task<DaybookResponse> CreateReceiptDaybookAsync(Guid organisationId, CreateReceiptDaybookRequest request);
    Task<DaybookResponse> CreatePaymentDaybookAsync(Guid organisationId, CreatePaymentDaybookRequest request);
    // Simplified methods — GL accounts auto-resolved from org defaults / product catalogue
    Task<DaybookResponse> CreateSimpleSalesDaybookAsync(Guid organisationId, SimpleInvoiceRequest request);
    Task<DaybookResponse> CreateSimpleSalesReturnDaybookAsync(Guid organisationId, SimpleInvoiceRequest request);
    Task<DaybookResponse> CreateSimplePurchaseDaybookAsync(Guid organisationId, SimpleInvoiceRequest request);
    Task<DaybookResponse> CreateSimplePurchaseReturnDaybookAsync(Guid organisationId, SimpleInvoiceRequest request);
    Task<DaybookResponse> GetDaybookEntryAsync(Guid entryId);
    Task<IEnumerable<DaybookResponse>> GetDaybookEntriesByOrganisationAsync(Guid organisationId, DateTime? fromDate = null, DateTime? toDate = null);
    Task PostDaybookEntryAsync(Guid entryId);
    Task DeleteDaybookEntryAsync(Guid entryId);
    Task<DaybookResponse> UpdateJournalLineAccountAsync(Guid entryId, Guid lineId, Guid newGLAccountId);
}

// ---- Sales Daybook ----

public class CreateSalesDaybookRequest
{
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    // Credit payment: either CustomerId (with a ControlAccountId) or ReceivableAccountId must be provided
    public Guid? CustomerId { get; set; }
    public Guid? ReceivableAccountId { get; set; }
    // Immediate payment: bypasses AR — the chosen asset account is debited directly
    public bool ImmediatePayment { get; set; } = false;
    public Guid? ImmediatePaymentAccountId { get; set; }
    // Required when any line has VatAmount > 0
    public Guid? VatAccountId { get; set; }
    [Required]
    [MinLength(1, ErrorMessage = "At least 1 sales line is required")]
    public List<SalesDaybookLine> Lines { get; set; } = new();
}

public class SalesDaybookLine
{
    [Required]
    public string Description { get; set; } = string.Empty;
    [Range(0.01, double.MaxValue, ErrorMessage = "Net amount must be greater than zero")]
    public decimal NetAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal VatAmount { get; set; }
    public Guid RevenueAccountId { get; set; }
}

// ---- Sales Return Daybook ----

public class CreateSalesReturnDaybookRequest
{
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    // Credit payment: either CustomerId (with a ControlAccountId) or ReceivableAccountId must be provided
    public Guid? CustomerId { get; set; }
    public Guid? ReceivableAccountId { get; set; }
    // Immediate payment: bypasses AR — the chosen asset account is credited directly (cash/bank refunded)
    public bool ImmediatePayment { get; set; } = false;
    public Guid? ImmediatePaymentAccountId { get; set; }
    // Required when any line has VatAmount > 0
    public Guid? VatAccountId { get; set; }
    [Required]
    [MinLength(1, ErrorMessage = "At least 1 return line is required")]
    public List<SalesReturnLine> Lines { get; set; } = new();
}

public class SalesReturnLine
{
    [Required]
    public string Description { get; set; } = string.Empty;
    [Range(0.01, double.MaxValue, ErrorMessage = "Net amount must be greater than zero")]
    public decimal NetAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal VatAmount { get; set; }
    // The expense account to debit (e.g. Sales Returns & Allowances)
    public Guid ExpenseAccountId { get; set; }
}

// ---- Purchase Daybook ----

public class CreatePurchaseDaybookRequest
{
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    // Credit payment: either SupplierId (with a ControlAccountId) or PayableAccountId must be provided
    public Guid? SupplierId { get; set; }
    public Guid? PayableAccountId { get; set; }
    // Immediate payment: bypasses AP — the chosen asset account is credited directly (cash/bank paid out)
    public bool ImmediatePayment { get; set; } = false;
    public Guid? ImmediatePaymentAccountId { get; set; }
    // Required when any line has VatAmount > 0
    public Guid? VatAccountId { get; set; }
    [Required]
    [MinLength(1, ErrorMessage = "At least 1 purchase line is required")]
    public List<PurchaseDaybookLine> Lines { get; set; } = new();
}

public class PurchaseDaybookLine
{
    [Required]
    public string Description { get; set; } = string.Empty;
    [Range(0.01, double.MaxValue, ErrorMessage = "Net amount must be greater than zero")]
    public decimal NetAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal VatAmount { get; set; }
    public Guid ExpenseAccountId { get; set; }
}

// ---- Receipt Daybook (money received into bank) ----

public class CreateReceiptDaybookRequest
{
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    // Bank/cash GL account to debit
    public Guid BankAccountId { get; set; }
    // AR or Revenue GL account to credit
    public Guid CreditAccountId { get; set; }
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }
    // Optional: link this receipt to a specific sales invoice
    public Guid? LinkedDaybookEntryId { get; set; }
}

// ---- Payment Daybook (money paid out of bank) ----

public class CreatePaymentDaybookRequest
{
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public Guid? SupplierId { get; set; }
    // AP or Expense GL account to debit
    public Guid DebitAccountId { get; set; }
    // Bank/cash GL account to credit
    public Guid BankAccountId { get; set; }
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }
    // Optional: link this payment to a specific purchase invoice
    public Guid? LinkedDaybookEntryId { get; set; }
}

// ---- Manual Journal ----

public class CreateDaybookRequest
{
    [Required]
    [RegularExpression("^(Sales|Purchase|Journal|Bank|Receipt|Payment)$")]
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

public class UpdateJournalLineRequest
{
    public Guid GLAccountId { get; set; }
}

public class DaybookResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public bool IsPosted { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? LinkedDaybookEntryId { get; set; }
    public string? LinkedReferenceNumber { get; set; }
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
    Task<ProfitAndLossResponse> GetProfitAndLossAsync(Guid organisationId, DateTime fromDate, DateTime toDate);
    Task<BalanceSheetResponse> GetBalanceSheetAsync(Guid organisationId, DateTime asOfDate);
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

public class ProfitAndLossResponse
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<PnLLineResponse> Revenue { get; set; } = new();
    public List<PnLLineResponse> CostOfSales { get; set; } = new();
    public decimal GrossProfit { get; set; }
    public List<PnLLineResponse> OperatingExpenses { get; set; } = new();
    public List<PnLLineResponse> FinanceCosts { get; set; } = new();
    public decimal NetProfit { get; set; }
}

public class PnLLineResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class BalanceSheetResponse
{
    public DateTime AsOfDate { get; set; }
    public List<BalanceSheetLineResponse> CurrentAssets { get; set; } = new();
    public List<BalanceSheetLineResponse> NonCurrentAssets { get; set; } = new();
    public decimal TotalAssets { get; set; }
    public List<BalanceSheetLineResponse> CurrentLiabilities { get; set; } = new();
    public List<BalanceSheetLineResponse> NonCurrentLiabilities { get; set; } = new();
    public decimal TotalLiabilities { get; set; }
    public List<BalanceSheetLineResponse> Equity { get; set; } = new();
    public decimal CurrentYearProfit { get; set; }
    public decimal TotalEquity { get; set; }
}

public class BalanceSheetLineResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public interface ICustomerSupplierService
{
    Task<CustomerResponse> CreateCustomerAsync(Guid organisationId, CreateCustomerRequest request);
    Task<CustomerResponse> GetCustomerAsync(Guid customerId);
    Task<IEnumerable<CustomerResponse>> GetCustomersByOrganisationAsync(Guid organisationId);
    Task<CustomerResponse> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request);
    Task DeleteCustomerAsync(Guid customerId);
    Task<CustomerLedgerResponse> GetCustomerLedgerAsync(Guid organisationId, Guid customerId);
    Task<IEnumerable<OutstandingInvoiceResponse>> GetOutstandingInvoicesAsync(Guid organisationId);

    Task<SupplierResponse> CreateSupplierAsync(Guid organisationId, CreateSupplierRequest request);
    Task<SupplierResponse> GetSupplierAsync(Guid supplierId);
    Task<IEnumerable<SupplierResponse>> GetSuppliersByOrganisationAsync(Guid organisationId);
    Task<SupplierResponse> UpdateSupplierAsync(Guid supplierId, UpdateSupplierRequest request);
    Task DeleteSupplierAsync(Guid supplierId);
    Task<SupplierLedgerResponse> GetSupplierLedgerAsync(Guid organisationId, Guid supplierId);
    Task<IEnumerable<OutstandingInvoiceResponse>> GetOutstandingBillsAsync(Guid organisationId);
}

// ---- Subsidiary Ledger (customer/supplier account breakdown) ----

public class LedgerLineResponse
{
    public Guid EntryId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
    public bool IsPosted { get; set; }
    public Guid? LinkedEntryId { get; set; }
    public string? LinkedReference { get; set; }
}

public class CustomerLedgerResponse
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<LedgerLineResponse> Lines { get; set; } = new();
    public decimal ClosingBalance { get; set; }
}

public class SupplierLedgerResponse
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public List<LedgerLineResponse> Lines { get; set; } = new();
    public decimal ClosingBalance { get; set; }
}

public class OutstandingInvoiceResponse
{
    public Guid EntryId { get; set; }
    public DateTime Date { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal InvoiceTotal { get; set; }
    public decimal TotalSettled { get; set; }
    public decimal Outstanding { get; set; }
    public bool IsPosted { get; set; }
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

// ---- Products & Services ----

public interface IProductService
{
    Task<ProductServiceResponse> CreateAsync(Guid organisationId, CreateProductServiceRequest request);
    Task<ProductServiceResponse> GetAsync(Guid productId);
    Task<IEnumerable<ProductServiceResponse>> GetByOrganisationAsync(Guid organisationId);
    Task<ProductServiceResponse> UpdateAsync(Guid productId, UpdateProductServiceRequest request);
    Task DeleteAsync(Guid productId);
}

public class CreateProductServiceRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    [StringLength(50)]
    public string? Code { get; set; }
    public string? Description { get; set; }
    [Range(0, double.MaxValue)]
    public decimal DefaultSalePrice { get; set; }
    [Range(0, double.MaxValue)]
    public decimal DefaultPurchasePrice { get; set; }
    [Required]
    [RegularExpression("^(standard|reduced|zero|exempt)$")]
    public string VatTreatment { get; set; } = "standard";
}

public class UpdateProductServiceRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    [StringLength(50)]
    public string? Code { get; set; }
    public string? Description { get; set; }
    [Range(0, double.MaxValue)]
    public decimal DefaultSalePrice { get; set; }
    [Range(0, double.MaxValue)]
    public decimal DefaultPurchasePrice { get; set; }
    [Required]
    [RegularExpression("^(standard|reduced|zero|exempt)$")]
    public string VatTreatment { get; set; } = "standard";
    public bool IsActive { get; set; }
}

public class ProductServiceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public decimal DefaultSalePrice { get; set; }
    public decimal DefaultPurchasePrice { get; set; }
    public string VatTreatment { get; set; } = "standard";
    public bool IsActive { get; set; }
}

// ---- Simplified invoice request (no GL account selection) ----

public class SimpleInvoiceRequest
{
    public string? ReferenceNumber { get; set; }
    [Required]
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    // Immediate payment: bypasses AR/AP — the chosen asset account is used directly
    public bool ImmediatePayment { get; set; } = false;
    public Guid? ImmediatePaymentAccountId { get; set; }
    // For returns: link back to the original sales/purchase entry
    public Guid? LinkedDaybookEntryId { get; set; }
    [Required]
    [MinLength(1, ErrorMessage = "At least 1 line is required")]
    public List<SimpleInvoiceLine> Lines { get; set; } = new();
}

public class SimpleInvoiceLine
{
    public Guid? ProductId { get; set; }
    public string? Description { get; set; }
    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; } = 1;
    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }
    [Required]
    [RegularExpression("^(standard|reduced|zero|exempt)$")]
    public string VatTreatment { get; set; } = "standard";
}
