using AccountingApp.Data;
using AccountingApp.Exceptions;
using AccountingApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingApp.Services;

public class GLAccountService : IGLAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GLAccountService> _logger;

    public GLAccountService(ApplicationDbContext context, ILogger<GLAccountService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GLAccountResponse> CreateAccountAsync(Guid organisationId, CreateGLAccountRequest request)
    {
        _logger.LogDebug("Creating GL account with code {Code} for organisation {OrganisationId}", request.Code, organisationId);

        var duplicate = await _context.GLAccounts
            .AnyAsync(a => a.OrganisationId == organisationId && a.Code == request.Code && a.IsActive);
        if (duplicate)
        {
            _logger.LogWarning("Duplicate GL account code {Code} attempted for organisation {OrganisationId}", request.Code, organisationId);
            throw new DuplicateResourceException("GL Account", "Code", request.Code);
        }

        var account = new GLAccount
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            SubType = request.SubType,
            OpeningBalance = request.OpeningBalance,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.GLAccounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("GL account {AccountCode} ({AccountId}) created for organisation {OrganisationId}", account.Code, account.Id, organisationId);

        return MapToResponse(account, request.OpeningBalance);
    }

    public async Task<GLAccountResponse> GetAccountAsync(Guid accountId)
    {
        var account = await _context.GLAccounts.FindAsync(accountId);
        if (account == null)
        {
            _logger.LogWarning("GL account {AccountId} not found", accountId);
            throw new ResourceNotFoundException("GL Account", accountId.ToString());
        }

        var balance = await CalculateAccountBalance(accountId);
        return MapToResponse(account, balance);
    }

    public async Task<IEnumerable<GLAccountResponse>> GetAccountsByOrganisationAsync(Guid organisationId)
    {
        _logger.LogDebug("Fetching GL accounts for organisation {OrganisationId}", organisationId);

        var accounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive)
            .ToListAsync();

        var responses = new List<GLAccountResponse>();
        foreach (var account in accounts)
        {
            var balance = await CalculateAccountBalance(account.Id);
            responses.Add(MapToResponse(account, balance));
        }

        return responses;
    }

    public async Task<GLAccountResponse> UpdateAccountAsync(Guid accountId, UpdateGLAccountRequest request)
    {
        var account = await _context.GLAccounts.FindAsync(accountId);
        if (account == null)
        {
            _logger.LogWarning("Update attempted on non-existent GL account {AccountId}", accountId);
            throw new ResourceNotFoundException("GL Account", accountId.ToString());
        }

        account.Name = request.Name;
        account.Description = request.Description;
        account.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        _logger.LogInformation("GL account {AccountCode} ({AccountId}) updated", account.Code, account.Id);

        var balance = await CalculateAccountBalance(accountId);
        return MapToResponse(account, balance);
    }

    public async Task DeleteAccountAsync(Guid accountId)
    {
        var account = await _context.GLAccounts.FindAsync(accountId);
        if (account == null)
        {
            _logger.LogWarning("Delete attempted on non-existent GL account {AccountId}", accountId);
            throw new ResourceNotFoundException("GL Account", accountId.ToString());
        }

        var hasPostedEntries = await _context.JournalEntries
            .AnyAsync(je => je.GLAccountId == accountId && je.DaybookEntry!.IsPosted);
        if (hasPostedEntries)
        {
            _logger.LogWarning("Delete attempted on GL account {AccountId} with posted entries", accountId);
            throw new BusinessRuleException(
                $"Cannot delete GL account '{account.Code}' because it has posted journal entries.",
                "ACCOUNT_HAS_POSTED_ENTRIES");
        }

        account.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("GL account {AccountCode} ({AccountId}) soft-deleted", account.Code, account.Id);
    }

    private async Task<decimal> CalculateAccountBalance(Guid accountId)
    {
        var account = await _context.GLAccounts.FindAsync(accountId);
        if (account == null) return 0;

        var journalEntries = await _context.JournalEntries
            .Where(je => je.GLAccountId == accountId)
            .ToListAsync();

        decimal totalDebits = journalEntries.Sum(je => je.DebitAmount);
        decimal totalCredits = journalEntries.Sum(je => je.CreditAmount);

        decimal balance = account.OpeningBalance;
        if (account.Type == "Asset" || account.Type == "Expense")
        {
            balance = account.OpeningBalance + totalDebits - totalCredits;
        }
        else if (account.Type == "Liability" || account.Type == "Equity" || account.Type == "Revenue")
        {
            balance = account.OpeningBalance + totalCredits - totalDebits;
        }

        return balance;
    }

    private GLAccountResponse MapToResponse(GLAccount account, decimal balance)
    {
        return new GLAccountResponse
        {
            Id = account.Id,
            Code = account.Code,
            Name = account.Name,
            Description = account.Description,
            Type = account.Type,
            SubType = account.SubType,
            Balance = balance,
            IsActive = account.IsActive
        };
    }
}

public class DaybookService : IDaybookService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DaybookService> _logger;

    public DaybookService(ApplicationDbContext context, ILogger<DaybookService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DaybookResponse> CreateDaybookEntryAsync(Guid organisationId, CreateDaybookRequest request)
    {
        _logger.LogDebug("Creating daybook entry of type {Type} for organisation {OrganisationId}", request.Type, organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
        {
            _logger.LogWarning("Daybook entry creation rejected: future date {EntryDate}", request.EntryDate);
            throw new ValidationException("Entry date cannot be in the future.");
        }

        if (request.Lines == null || request.Lines.Count < 2)
        {
            throw new ValidationException("At least 2 journal lines are required.");
        }

        // Validate all GL accounts exist
        foreach (var line in request.Lines)
        {
            var accountExists = await _context.GLAccounts.AnyAsync(a => a.Id == line.GLAccountId && a.IsActive);
            if (!accountExists)
            {
                _logger.LogWarning("Daybook entry creation rejected: GL account {GLAccountId} not found", line.GLAccountId);
                throw new ResourceNotFoundException("GL Account", line.GLAccountId.ToString());
            }
        }

        var daybookEntry = new DaybookEntry
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Type = request.Type,
            ReferenceNumber = request.ReferenceNumber,
            EntryDate = request.EntryDate,
            Description = request.Description,
            IsPosted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid() // Should be set from current user
        };

        _context.DaybookEntries.Add(daybookEntry);

        foreach (var line in request.Lines)
        {
            var journalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                DaybookEntryId = daybookEntry.Id,
                GLAccountId = line.GLAccountId,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                NarrationLine = line.NarrationLine
            };

            _context.JournalEntries.Add(journalEntry);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Daybook entry {EntryId} (type: {Type}) created for organisation {OrganisationId}", daybookEntry.Id, daybookEntry.Type, organisationId);

        return await MapToDaybookResponse(daybookEntry);
    }

    public async Task<DaybookResponse> GetDaybookEntryAsync(Guid entryId)
    {
        var daybookEntry = await _context.DaybookEntries
            .Include(de => de.JournalEntries)
            .FirstOrDefaultAsync(de => de.Id == entryId);

        if (daybookEntry == null)
        {
            _logger.LogWarning("Daybook entry {EntryId} not found", entryId);
            throw new ResourceNotFoundException("Daybook Entry", entryId.ToString());
        }

        return await MapToDaybookResponse(daybookEntry);
    }

    public async Task<IEnumerable<DaybookResponse>> GetDaybookEntriesByOrganisationAsync(Guid organisationId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            throw new ValidationException("toDate must be greater than or equal to fromDate.");
        }

        _logger.LogDebug("Fetching daybook entries for organisation {OrganisationId}", organisationId);

        var query = _context.DaybookEntries
            .Include(de => de.JournalEntries)
            .Where(de => de.OrganisationId == organisationId);

        if (fromDate.HasValue)
            query = query.Where(de => de.EntryDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(de => de.EntryDate <= toDate.Value);

        var daybookEntries = await query.OrderByDescending(de => de.EntryDate).ToListAsync();

        var responses = new List<DaybookResponse>();
        foreach (var entry in daybookEntries)
        {
            responses.Add(await MapToDaybookResponse(entry));
        }

        return responses;
    }

    public async Task PostDaybookEntryAsync(Guid entryId)
    {
        var daybookEntry = await _context.DaybookEntries.FindAsync(entryId);
        if (daybookEntry == null)
        {
            _logger.LogWarning("Post attempted on non-existent daybook entry {EntryId}", entryId);
            throw new ResourceNotFoundException("Daybook Entry", entryId.ToString());
        }

        if (daybookEntry.IsPosted)
        {
            _logger.LogWarning("Post attempted on already-posted daybook entry {EntryId}", entryId);
            throw new BusinessRuleException("Daybook entry is already posted.", "ENTRY_ALREADY_POSTED");
        }

        var journalEntries = await _context.JournalEntries
            .Where(je => je.DaybookEntryId == entryId)
            .ToListAsync();

        decimal totalDebits = journalEntries.Sum(je => je.DebitAmount);
        decimal totalCredits = journalEntries.Sum(je => je.CreditAmount);

        if (Math.Abs(totalDebits - totalCredits) > 0.01m)
        {
            _logger.LogWarning("Post rejected for entry {EntryId}: debits ({Debits}) do not equal credits ({Credits})", entryId, totalDebits, totalCredits);
            throw new BusinessRuleException(
                $"Cannot post entry: total debits ({totalDebits:F2}) must equal total credits ({totalCredits:F2}).",
                "UNBALANCED_JOURNAL_ENTRY");
        }

        daybookEntry.IsPosted = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Daybook entry {EntryId} posted successfully", entryId);
    }

    public async Task DeleteDaybookEntryAsync(Guid entryId)
    {
        var daybookEntry = await _context.DaybookEntries.FindAsync(entryId);
        if (daybookEntry == null)
        {
            _logger.LogWarning("Delete attempted on non-existent daybook entry {EntryId}", entryId);
            throw new ResourceNotFoundException("Daybook Entry", entryId.ToString());
        }

        if (daybookEntry.IsPosted)
        {
            _logger.LogWarning("Delete attempted on posted daybook entry {EntryId}", entryId);
            throw new OperationFailedException(
                "Cannot delete a posted daybook entry.",
                "DeleteDaybookEntry",
                "Entry is already posted");
        }

        var journalEntries = await _context.JournalEntries
            .Where(je => je.DaybookEntryId == entryId)
            .ToListAsync();

        _context.JournalEntries.RemoveRange(journalEntries);
        _context.DaybookEntries.Remove(daybookEntry);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Daybook entry {EntryId} deleted", entryId);
    }

    private async Task<DaybookResponse> MapToDaybookResponse(DaybookEntry daybookEntry)
    {
        var journalEntries = await _context.JournalEntries
            .Where(je => je.DaybookEntryId == daybookEntry.Id)
            .ToListAsync();

        var lines = new List<JournalLineResponse>();
        foreach (var entry in journalEntries)
        {
            var glAccount = await _context.GLAccounts.FindAsync(entry.GLAccountId);
            lines.Add(new JournalLineResponse
            {
                Id = entry.Id,
                GLAccountId = entry.GLAccountId,
                GLAccountName = glAccount?.Name ?? "Unknown",
                DebitAmount = entry.DebitAmount,
                CreditAmount = entry.CreditAmount,
                NarrationLine = entry.NarrationLine
            });
        }

        return new DaybookResponse
        {
            Id = daybookEntry.Id,
            Type = daybookEntry.Type,
            ReferenceNumber = daybookEntry.ReferenceNumber,
            EntryDate = daybookEntry.EntryDate,
            Description = daybookEntry.Description,
            IsPosted = daybookEntry.IsPosted,
            Lines = lines
        };
    }
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReportService> _logger;

    public ReportService(ApplicationDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TrialBalanceResponse> GetTrialBalanceAsync(Guid organisationId, DateTime asOfDate)
    {
        _logger.LogInformation("Generating trial balance for organisation {OrganisationId} as of {AsOfDate}", organisationId, asOfDate);

        var accounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive)
            .ToListAsync();

        var lines = new List<TrialBalanceLineResponse>();
        decimal totalDebits = 0;
        decimal totalCredits = 0;

        foreach (var account in accounts)
        {
            var journalEntries = await _context.JournalEntries
                .AsNoTracking()
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.EntryDate <= asOfDate &&
                             je.DaybookEntry!.IsPosted)
                .ToListAsync();

            decimal debit = journalEntries.Sum(je => je.DebitAmount);
            decimal credit = journalEntries.Sum(je => je.CreditAmount);

            decimal balance = account.OpeningBalance;
            if (account.Type == "Asset" || account.Type == "Expense")
            {
                balance = account.OpeningBalance + debit - credit;
            }
            else
            {
                balance = account.OpeningBalance + credit - debit;
            }

            if (Math.Abs(balance) > 0.01m)
            {
                if (account.Type == "Asset" || account.Type == "Expense")
                {
                    lines.Add(new TrialBalanceLineResponse
                    {
                        AccountId = account.Id,
                        Code = account.Code,
                        Name = account.Name,
                        Type = account.Type,
                        Debit = balance > 0 ? balance : 0,
                        Credit = balance < 0 ? Math.Abs(balance) : 0
                    });
                }
                else
                {
                    lines.Add(new TrialBalanceLineResponse
                    {
                        AccountId = account.Id,
                        Code = account.Code,
                        Name = account.Name,
                        Type = account.Type,
                        Debit = balance < 0 ? Math.Abs(balance) : 0,
                        Credit = balance > 0 ? balance : 0
                    });
                }

                totalDebits += lines.Last().Debit;
                totalCredits += lines.Last().Credit;
            }
        }

        _logger.LogInformation("Trial balance generated: {LineCount} accounts, debits={TotalDebits}, credits={TotalCredits}", lines.Count, totalDebits, totalCredits);

        return new TrialBalanceResponse
        {
            AsOfDate = asOfDate,
            Lines = lines.OrderBy(l => l.Code).ToList(),
            TotalDebits = totalDebits,
            TotalCredits = totalCredits
        };
    }

    public async Task<IEnumerable<TAccountResponse>> GetTAccountsAsync(Guid organisationId, DateTime asOfDate)
    {
        _logger.LogInformation("Generating T-accounts for organisation {OrganisationId} as of {AsOfDate}", organisationId, asOfDate);

        var accounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive)
            .ToListAsync();

        var responses = new List<TAccountResponse>();
        foreach (var account in accounts)
        {
            var tAccount = await GetTAccountAsync(account.Id, asOfDate);
            responses.Add(tAccount);
        }

        return responses;
    }

    public async Task<TAccountResponse> GetTAccountAsync(Guid accountId, DateTime asOfDate)
    {
        var account = await _context.GLAccounts.FindAsync(accountId);
        if (account == null)
        {
            _logger.LogWarning("T-account requested for non-existent GL account {AccountId}", accountId);
            throw new ResourceNotFoundException("GL Account", accountId.ToString());
        }

        var journalEntries = await _context.JournalEntries
            .AsNoTracking()
            .Where(je => je.GLAccountId == accountId &&
                         je.DaybookEntry!.EntryDate <= asOfDate &&
                         je.DaybookEntry!.IsPosted)
            .OrderBy(je => je.DaybookEntry!.EntryDate)
            .ToListAsync();

        var lines = new List<TAccountLineResponse>();
        decimal runningBalance = account.OpeningBalance;

        foreach (var je in journalEntries)
        {
            var daybookEntry = await _context.DaybookEntries.FindAsync(je.DaybookEntryId);
            if (daybookEntry == null) continue;

            lines.Add(new TAccountLineResponse
            {
                Date = daybookEntry.EntryDate,
                Reference = daybookEntry.ReferenceNumber ?? "",
                Description = daybookEntry.Description ?? je.NarrationLine ?? "",
                Debit = je.DebitAmount > 0 ? je.DebitAmount : null,
                Credit = je.CreditAmount > 0 ? je.CreditAmount : null
            });

            if (account.Type == "Asset" || account.Type == "Expense")
            {
                runningBalance += je.DebitAmount - je.CreditAmount;
            }
            else
            {
                runningBalance = runningBalance + je.CreditAmount - je.DebitAmount;
            }
        }

        return new TAccountResponse
        {
            AccountId = account.Id,
            Code = account.Code,
            Name = account.Name,
            OpeningBalance = account.OpeningBalance,
            Entries = lines,
            ClosingBalance = runningBalance
        };
    }

    public async Task<GeneralLedgerResponse> GetGeneralLedgerAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        if (toDate < fromDate)
        {
            throw new ValidationException("toDate must be greater than or equal to fromDate.");
        }

        _logger.LogInformation("Generating general ledger for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var journalEntries = await _context.JournalEntries
            .AsNoTracking()
            .Where(je => je.GLAccount!.OrganisationId == organisationId &&
                         je.DaybookEntry!.EntryDate >= fromDate &&
                         je.DaybookEntry!.EntryDate <= toDate &&
                         je.DaybookEntry!.IsPosted)
            .OrderBy(je => je.GLAccount!.Code)
            .ThenBy(je => je.DaybookEntry!.EntryDate)
            .ToListAsync();

        var entries = new List<GLEntryResponse>();
        foreach (var je in journalEntries)
        {
            var glAccount = await _context.GLAccounts.FindAsync(je.GLAccountId);
            var daybookEntry = await _context.DaybookEntries.FindAsync(je.DaybookEntryId);

            if (glAccount != null && daybookEntry != null)
            {
                entries.Add(new GLEntryResponse
                {
                    Date = daybookEntry.EntryDate,
                    Reference = daybookEntry.ReferenceNumber ?? "",
                    AccountCode = glAccount.Code,
                    Description = je.NarrationLine ?? daybookEntry.Description ?? "",
                    Debit = je.DebitAmount,
                    Credit = je.CreditAmount
                });
            }
        }

        return new GeneralLedgerResponse { Entries = entries };
    }

    public async Task<ProfitAndLossResponse> GetProfitAndLossAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating P&L for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var accounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive &&
                        (a.Type == "Revenue" || a.Type == "Expense"))
            .OrderBy(a => a.Code)
            .ToListAsync();

        var revenue = new List<PnLLineResponse>();
        var costOfSales = new List<PnLLineResponse>();
        var operatingExpenses = new List<PnLLineResponse>();
        var financeCosts = new List<PnLLineResponse>();

        foreach (var account in accounts)
        {
            var entries = await _context.JournalEntries
                .AsNoTracking()
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.EntryDate >= fromDate &&
                             je.DaybookEntry!.EntryDate <= toDate &&
                             je.DaybookEntry!.IsPosted)
                .ToListAsync();

            decimal debits = entries.Sum(je => je.DebitAmount);
            decimal credits = entries.Sum(je => je.CreditAmount);

            if (account.Type == "Revenue")
            {
                decimal amount = credits - debits;
                if (amount != 0)
                    revenue.Add(new PnLLineResponse { Code = account.Code, Name = account.Name, SubType = account.SubType ?? "", Amount = amount });
            }
            else
            {
                decimal amount = debits - credits;
                if (amount != 0)
                {
                    var line = new PnLLineResponse { Code = account.Code, Name = account.Name, SubType = account.SubType ?? "", Amount = amount };
                    if (account.SubType == "Cost of Sales")
                        costOfSales.Add(line);
                    else if (account.SubType == "Finance Cost")
                        financeCosts.Add(line);
                    else
                        operatingExpenses.Add(line);
                }
            }
        }

        decimal totalRevenue = revenue.Sum(r => r.Amount);
        decimal totalCoS = costOfSales.Sum(c => c.Amount);
        decimal grossProfit = totalRevenue - totalCoS;
        decimal totalOpEx = operatingExpenses.Sum(o => o.Amount);
        decimal totalFinance = financeCosts.Sum(f => f.Amount);
        decimal netProfit = grossProfit - totalOpEx - totalFinance;

        return new ProfitAndLossResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            Revenue = revenue,
            CostOfSales = costOfSales,
            GrossProfit = grossProfit,
            OperatingExpenses = operatingExpenses,
            FinanceCosts = financeCosts,
            NetProfit = netProfit
        };
    }

    public async Task<BalanceSheetResponse> GetBalanceSheetAsync(Guid organisationId, DateTime asOfDate)
    {
        _logger.LogInformation("Generating balance sheet for organisation {OrganisationId} as of {AsOfDate}", organisationId, asOfDate);

        var accounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync();

        var currentAssets = new List<BalanceSheetLineResponse>();
        var nonCurrentAssets = new List<BalanceSheetLineResponse>();
        var currentLiabilities = new List<BalanceSheetLineResponse>();
        var nonCurrentLiabilities = new List<BalanceSheetLineResponse>();
        var equity = new List<BalanceSheetLineResponse>();
        decimal currentYearProfit = 0;

        foreach (var account in accounts)
        {
            var entries = await _context.JournalEntries
                .AsNoTracking()
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.EntryDate <= asOfDate &&
                             je.DaybookEntry!.IsPosted)
                .ToListAsync();

            decimal debits = entries.Sum(je => je.DebitAmount);
            decimal credits = entries.Sum(je => je.CreditAmount);
            decimal balance = (account.Type == "Asset" || account.Type == "Expense")
                ? account.OpeningBalance + debits - credits
                : account.OpeningBalance + credits - debits;

            if (account.Type == "Revenue")
            {
                currentYearProfit += balance;
            }
            else if (account.Type == "Expense")
            {
                currentYearProfit -= balance;
            }
            else if (Math.Abs(balance) > 0.005m)
            {
                var line = new BalanceSheetLineResponse { Code = account.Code, Name = account.Name, Amount = balance };
                bool isNonCurrent = !string.IsNullOrEmpty(account.SubType) && account.SubType.Contains("Non");
                switch (account.Type)
                {
                    case "Asset":
                        (isNonCurrent ? nonCurrentAssets : currentAssets).Add(line);
                        break;
                    case "Liability":
                        (isNonCurrent ? nonCurrentLiabilities : currentLiabilities).Add(line);
                        break;
                    case "Equity":
                        equity.Add(line);
                        break;
                }
            }
        }

        decimal totalAssets = currentAssets.Sum(a => a.Amount) + nonCurrentAssets.Sum(a => a.Amount);
        decimal totalLiabilities = currentLiabilities.Sum(l => l.Amount) + nonCurrentLiabilities.Sum(l => l.Amount);
        decimal totalEquity = equity.Sum(e => e.Amount) + currentYearProfit;

        return new BalanceSheetResponse
        {
            AsOfDate = asOfDate,
            CurrentAssets = currentAssets,
            NonCurrentAssets = nonCurrentAssets,
            TotalAssets = totalAssets,
            CurrentLiabilities = currentLiabilities,
            NonCurrentLiabilities = nonCurrentLiabilities,
            TotalLiabilities = totalLiabilities,
            Equity = equity,
            CurrentYearProfit = currentYearProfit,
            TotalEquity = totalEquity
        };
    }
}

public class CustomerSupplierService : ICustomerSupplierService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CustomerSupplierService> _logger;

    public CustomerSupplierService(ApplicationDbContext context, ILogger<CustomerSupplierService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CustomerResponse> CreateCustomerAsync(Guid organisationId, CreateCustomerRequest request)
    {
        _logger.LogDebug("Creating customer {Name} for organisation {OrganisationId}", request.Name, organisationId);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
            CreditLimit = request.CreditLimit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} ({Name}) created for organisation {OrganisationId}", customer.Id, customer.Name, organisationId);

        return MapToCustomerResponse(customer, 0);
    }

    public async Task<CustomerResponse> GetCustomerAsync(Guid customerId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            _logger.LogWarning("Customer {CustomerId} not found", customerId);
            throw new ResourceNotFoundException("Customer", customerId.ToString());
        }

        var balance = await GetCustomerBalance(customerId);
        return MapToCustomerResponse(customer, balance);
    }

    public async Task<IEnumerable<CustomerResponse>> GetCustomersByOrganisationAsync(Guid organisationId)
    {
        _logger.LogDebug("Fetching customers for organisation {OrganisationId}", organisationId);

        var customers = await _context.Customers
            .Where(c => c.OrganisationId == organisationId && c.IsActive)
            .ToListAsync();

        var responses = new List<CustomerResponse>();
        foreach (var customer in customers)
        {
            var balance = await GetCustomerBalance(customer.Id);
            responses.Add(MapToCustomerResponse(customer, balance));
        }

        return responses;
    }

    public async Task<CustomerResponse> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            _logger.LogWarning("Update attempted on non-existent customer {CustomerId}", customerId);
            throw new ResourceNotFoundException("Customer", customerId.ToString());
        }

        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.Address = request.Address;
        customer.City = request.City;
        customer.PostalCode = request.PostalCode;
        customer.Country = request.Country;
        customer.CreditLimit = request.CreditLimit;
        customer.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} ({Name}) updated", customer.Id, customer.Name);

        var balance = await GetCustomerBalance(customerId);
        return MapToCustomerResponse(customer, balance);
    }

    public async Task DeleteCustomerAsync(Guid customerId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            _logger.LogWarning("Delete attempted on non-existent customer {CustomerId}", customerId);
            throw new ResourceNotFoundException("Customer", customerId.ToString());
        }

        customer.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} ({Name}) soft-deleted", customer.Id, customer.Name);
    }

    public async Task<SupplierResponse> CreateSupplierAsync(Guid organisationId, CreateSupplierRequest request)
    {
        _logger.LogDebug("Creating supplier {Name} for organisation {OrganisationId}", request.Name, organisationId);

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
            CreditLimit = request.CreditLimit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Supplier {SupplierId} ({Name}) created for organisation {OrganisationId}", supplier.Id, supplier.Name, organisationId);

        return MapToSupplierResponse(supplier, 0);
    }

    public async Task<SupplierResponse> GetSupplierAsync(Guid supplierId)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            _logger.LogWarning("Supplier {SupplierId} not found", supplierId);
            throw new ResourceNotFoundException("Supplier", supplierId.ToString());
        }

        var balance = await GetSupplierBalance(supplierId);
        return MapToSupplierResponse(supplier, balance);
    }

    public async Task<IEnumerable<SupplierResponse>> GetSuppliersByOrganisationAsync(Guid organisationId)
    {
        _logger.LogDebug("Fetching suppliers for organisation {OrganisationId}", organisationId);

        var suppliers = await _context.Suppliers
            .Where(s => s.OrganisationId == organisationId && s.IsActive)
            .ToListAsync();

        var responses = new List<SupplierResponse>();
        foreach (var supplier in suppliers)
        {
            var balance = await GetSupplierBalance(supplier.Id);
            responses.Add(MapToSupplierResponse(supplier, balance));
        }

        return responses;
    }

    public async Task<SupplierResponse> UpdateSupplierAsync(Guid supplierId, UpdateSupplierRequest request)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            _logger.LogWarning("Update attempted on non-existent supplier {SupplierId}", supplierId);
            throw new ResourceNotFoundException("Supplier", supplierId.ToString());
        }

        supplier.Name = request.Name;
        supplier.Email = request.Email;
        supplier.Phone = request.Phone;
        supplier.Address = request.Address;
        supplier.City = request.City;
        supplier.PostalCode = request.PostalCode;
        supplier.Country = request.Country;
        supplier.CreditLimit = request.CreditLimit;
        supplier.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Supplier {SupplierId} ({Name}) updated", supplier.Id, supplier.Name);

        var balance = await GetSupplierBalance(supplierId);
        return MapToSupplierResponse(supplier, balance);
    }

    public async Task DeleteSupplierAsync(Guid supplierId)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            _logger.LogWarning("Delete attempted on non-existent supplier {SupplierId}", supplierId);
            throw new ResourceNotFoundException("Supplier", supplierId.ToString());
        }

        supplier.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Supplier {SupplierId} ({Name}) soft-deleted", supplier.Id, supplier.Name);
    }

    private async Task<decimal> GetCustomerBalance(Guid customerId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer?.ControlAccountId == null) return 0;

        var journalEntries = await _context.JournalEntries
            .Where(je => je.GLAccountId == customer.ControlAccountId)
            .ToListAsync();

        decimal balance = journalEntries.Sum(je => je.DebitAmount - je.CreditAmount);
        return balance;
    }

    private async Task<decimal> GetSupplierBalance(Guid supplierId)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier?.ControlAccountId == null) return 0;

        var journalEntries = await _context.JournalEntries
            .Where(je => je.GLAccountId == supplier.ControlAccountId)
            .ToListAsync();

        decimal balance = journalEntries.Sum(je => je.CreditAmount - je.DebitAmount);
        return balance;
    }

    private CustomerResponse MapToCustomerResponse(Customer customer, decimal balance)
    {
        return new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            Email = customer.Email,
            Phone = customer.Phone,
            Address = customer.Address,
            City = customer.City,
            PostalCode = customer.PostalCode,
            Country = customer.Country,
            CreditLimit = customer.CreditLimit,
            CurrentBalance = balance,
            IsActive = customer.IsActive
        };
    }

    private SupplierResponse MapToSupplierResponse(Supplier supplier, decimal balance)
    {
        return new SupplierResponse
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Email = supplier.Email,
            Phone = supplier.Phone,
            Address = supplier.Address,
            City = supplier.City,
            PostalCode = supplier.PostalCode,
            Country = supplier.Country,
            CreditLimit = supplier.CreditLimit,
            CurrentBalance = balance,
            IsActive = supplier.IsActive
        };
    }
}
