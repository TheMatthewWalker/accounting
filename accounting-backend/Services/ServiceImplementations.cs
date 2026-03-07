using AccountingApp.Data;
using AccountingApp.Exceptions;
using AccountingApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingApp.Services;

public class OrganisationService : IOrganisationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrganisationService> _logger;

    public OrganisationService(ApplicationDbContext context, ILogger<OrganisationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task DeleteOrganisationAsync(Guid organisationId)
    {
        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null)
            throw new ResourceNotFoundException("Organisation", organisationId.ToString());

        org.IsActive = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Organisation {OrganisationId} soft-deleted", organisationId);
    }

    public async Task<IEnumerable<MemberResponse>> GetMembersAsync(Guid organisationId)
    {
        var members = await _context.OrganisationMembers
            .Include(m => m.User)
            .Where(m => m.OrganisationId == organisationId && m.IsActive)
            .ToListAsync();

        return members.Select(m => new MemberResponse
        {
            MemberId = m.Id,
            UserId = m.UserId,
            Email = m.User?.Email ?? string.Empty,
            FirstName = m.User?.FirstName ?? string.Empty,
            LastName = m.User?.LastName ?? string.Empty,
            Role = m.Role,
            JoinedAt = m.JoinedAt
        });
    }

    public async Task<MemberResponse> UpdateMemberRoleAsync(Guid organisationId, Guid memberId, UpdateMemberRoleRequest request)
    {
        var member = await _context.OrganisationMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganisationId == organisationId && m.IsActive);

        if (member == null)
            throw new ResourceNotFoundException("Member", memberId.ToString());

        // Prevent demoting the last Owner
        if (member.Role == "Owner" && request.Role != "Owner")
        {
            var ownerCount = await _context.OrganisationMembers
                .CountAsync(m => m.OrganisationId == organisationId && m.Role == "Owner" && m.IsActive);
            if (ownerCount <= 1)
                throw new BusinessRuleException("Cannot change the role of the last Owner.", "LAST_OWNER");
        }

        member.Role = request.Role;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Member {MemberId} role updated to {Role} in organisation {OrganisationId}", memberId, request.Role, organisationId);

        return new MemberResponse
        {
            MemberId = member.Id,
            UserId = member.UserId,
            Email = member.User?.Email ?? string.Empty,
            FirstName = member.User?.FirstName ?? string.Empty,
            LastName = member.User?.LastName ?? string.Empty,
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };
    }

    public async Task RemoveMemberAsync(Guid organisationId, Guid memberId, Guid requestingUserId)
    {
        var member = await _context.OrganisationMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganisationId == organisationId && m.IsActive);

        if (member == null)
            throw new ResourceNotFoundException("Member", memberId.ToString());

        if (member.UserId == requestingUserId)
            throw new BusinessRuleException("You cannot remove yourself from the organisation.", "CANNOT_REMOVE_SELF");

        if (member.Role == "Owner")
        {
            var ownerCount = await _context.OrganisationMembers
                .CountAsync(m => m.OrganisationId == organisationId && m.Role == "Owner" && m.IsActive);
            if (ownerCount <= 1)
                throw new BusinessRuleException("Cannot remove the last Owner.", "LAST_OWNER");
        }

        member.IsActive = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Member {MemberId} removed from organisation {OrganisationId}", memberId, organisationId);
    }

    public async Task<InvitationResponse> CreateInvitationAsync(Guid organisationId, Guid invitedByUserId, CreateInvitationRequest request)
    {
        var alreadyMember = await _context.OrganisationMembers
            .AnyAsync(m => m.OrganisationId == organisationId && m.User!.Email == request.Email && m.IsActive);
        if (alreadyMember)
            throw new DuplicateResourceException($"A user with email '{request.Email}' is already a member of this organisation.");

        var pendingInvite = await _context.OrganisationInvitations
            .AnyAsync(i => i.OrganisationId == organisationId && i.InvitedEmail == request.Email
                        && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);
        if (pendingInvite)
            throw new DuplicateResourceException($"A pending invitation for '{request.Email}' already exists.");

        var invitation = new OrganisationInvitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            InvitedEmail = request.Email,
            Role = request.Role,
            Token = Guid.NewGuid().ToString("N"), // 32-char alphanumeric token
            InvitedByUserId = invitedByUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsAccepted = false
        };

        _context.OrganisationInvitations.Add(invitation);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Invitation created for {Email} to join organisation {OrganisationId} with role {Role}", request.Email, organisationId, request.Role);

        return MapToInvitationResponse(invitation);
    }

    public async Task<IEnumerable<InvitationResponse>> GetInvitationsAsync(Guid organisationId)
    {
        var invitations = await _context.OrganisationInvitations
            .Where(i => i.OrganisationId == organisationId && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invitations.Select(MapToInvitationResponse);
    }

    public async Task CancelInvitationAsync(Guid organisationId, Guid invitationId)
    {
        var invitation = await _context.OrganisationInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganisationId == organisationId);

        if (invitation == null)
            throw new ResourceNotFoundException("Invitation", invitationId.ToString());

        _context.OrganisationInvitations.Remove(invitation);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Invitation {InvitationId} cancelled", invitationId);
    }

    public async Task<MemberResponse> AcceptInvitationAsync(string token, Guid acceptingUserId)
    {
        var invitation = await _context.OrganisationInvitations
            .FirstOrDefaultAsync(i => i.Token == token && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);

        if (invitation == null)
            throw new ResourceNotFoundException("Invitation token is invalid or has expired.");

        var alreadyMember = await _context.OrganisationMembers
            .AnyAsync(m => m.UserId == acceptingUserId && m.OrganisationId == invitation.OrganisationId && m.IsActive);
        if (alreadyMember)
            throw new BusinessRuleException("You are already a member of this organisation.", "ALREADY_MEMBER");

        var membership = new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = invitation.OrganisationId,
            UserId = acceptingUserId,
            Role = invitation.Role,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.OrganisationMembers.Add(membership);
        invitation.IsAccepted = true;
        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(acceptingUserId);
        _logger.LogInformation("User {UserId} accepted invitation to organisation {OrganisationId} with role {Role}", acceptingUserId, invitation.OrganisationId, invitation.Role);

        return new MemberResponse
        {
            MemberId = membership.Id,
            UserId = acceptingUserId,
            Email = user?.Email ?? string.Empty,
            FirstName = user?.FirstName ?? string.Empty,
            LastName = user?.LastName ?? string.Empty,
            Role = membership.Role,
            JoinedAt = membership.JoinedAt
        };
    }

    private static InvitationResponse MapToInvitationResponse(OrganisationInvitation i) => new()
    {
        Id = i.Id,
        InvitedEmail = i.InvitedEmail,
        Role = i.Role,
        Token = i.Token,
        ExpiresAt = i.ExpiresAt,
        IsAccepted = i.IsAccepted
    };
}

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

        decimal totalDebits = request.Lines.Sum(l => l.DebitAmount);
        decimal totalCredits = request.Lines.Sum(l => l.CreditAmount);

        if (Math.Abs(totalDebits - totalCredits) > 0.01m)
        {
            _logger.LogWarning("Daybook entry creation rejected: debits ({Debits}) do not equal credits ({Credits})", totalDebits, totalCredits);
            throw new ValidationException(
                $"Journal entry is not balanced: total debits ({totalDebits:F2}) must equal total credits ({totalCredits:F2}).");
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

    public async Task<DaybookResponse> CreateSalesDaybookAsync(Guid organisationId, CreateSalesDaybookRequest request)
    {
        _logger.LogDebug("Creating Sales daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        // Resolve the Accounts Receivable account
        Guid receivableAccountId;
        Guid? customerId = null;

        if (request.CustomerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
            if (customer == null)
                throw new ResourceNotFoundException("Customer", request.CustomerId.Value.ToString());
            customerId = customer.Id;
            if (customer.ControlAccountId.HasValue)
                receivableAccountId = customer.ControlAccountId.Value;
            else if (request.ReceivableAccountId.HasValue)
                receivableAccountId = request.ReceivableAccountId.Value;
            else
                throw new ValidationException("The specified customer has no control account linked. Please provide a ReceivableAccountId.");
        }
        else if (request.ReceivableAccountId.HasValue)
        {
            receivableAccountId = request.ReceivableAccountId.Value;
        }
        else
        {
            throw new ValidationException("Either CustomerId (with a linked AR account) or ReceivableAccountId must be specified.");
        }

        await ValidateGLAccount(receivableAccountId, "Receivable");

        bool hasVat = request.Lines.Any(l => l.VatAmount > 0);
        if (hasVat && !request.VatAccountId.HasValue)
            throw new ValidationException("VatAccountId is required when any line has a VAT amount.");
        if (request.VatAccountId.HasValue)
            await ValidateGLAccount(request.VatAccountId.Value, "VAT");

        foreach (var line in request.Lines)
            await ValidateGLAccount(line.RevenueAccountId, "Revenue");

        decimal totalAmount = request.Lines.Sum(l => l.NetAmount + l.VatAmount);

        var entry = BuildDaybookEntry(organisationId, "Sales", request.ReferenceNumber, request.EntryDate, request.Description, customerId, null);
        _context.DaybookEntries.Add(entry);

        // DR Receivable for the full invoice total
        AddJournalLine(entry.Id, receivableAccountId, debit: totalAmount, narration: request.Description);

        // CR Revenue (and VAT) per line
        foreach (var line in request.Lines)
        {
            AddJournalLine(entry.Id, line.RevenueAccountId, credit: line.NetAmount, narration: line.Description);
            if (line.VatAmount > 0)
                AddJournalLine(entry.Id, request.VatAccountId!.Value, credit: line.VatAmount, narration: $"VAT on {line.Description}");
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales daybook entry {EntryId} created for organisation {OrganisationId}", entry.Id, organisationId);
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreatePurchaseDaybookAsync(Guid organisationId, CreatePurchaseDaybookRequest request)
    {
        _logger.LogDebug("Creating Purchase daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        // Resolve the Accounts Payable account
        Guid payableAccountId;
        Guid? supplierId = null;

        if (request.SupplierId.HasValue)
        {
            var supplier = await _context.Suppliers.FindAsync(request.SupplierId.Value);
            if (supplier == null)
                throw new ResourceNotFoundException("Supplier", request.SupplierId.Value.ToString());
            supplierId = supplier.Id;
            if (supplier.ControlAccountId.HasValue)
                payableAccountId = supplier.ControlAccountId.Value;
            else if (request.PayableAccountId.HasValue)
                payableAccountId = request.PayableAccountId.Value;
            else
                throw new ValidationException("The specified supplier has no control account linked. Please provide a PayableAccountId.");
        }
        else if (request.PayableAccountId.HasValue)
        {
            payableAccountId = request.PayableAccountId.Value;
        }
        else
        {
            throw new ValidationException("Either SupplierId (with a linked AP account) or PayableAccountId must be specified.");
        }

        await ValidateGLAccount(payableAccountId, "Payable");

        bool hasVat = request.Lines.Any(l => l.VatAmount > 0);
        if (hasVat && !request.VatAccountId.HasValue)
            throw new ValidationException("VatAccountId is required when any line has a VAT amount.");
        if (request.VatAccountId.HasValue)
            await ValidateGLAccount(request.VatAccountId.Value, "VAT");

        foreach (var line in request.Lines)
            await ValidateGLAccount(line.ExpenseAccountId, "Expense");

        decimal totalAmount = request.Lines.Sum(l => l.NetAmount + l.VatAmount);

        var entry = BuildDaybookEntry(organisationId, "Purchase", request.ReferenceNumber, request.EntryDate, request.Description, null, supplierId);
        _context.DaybookEntries.Add(entry);

        // DR Expense (and VAT) per line
        foreach (var line in request.Lines)
        {
            AddJournalLine(entry.Id, line.ExpenseAccountId, debit: line.NetAmount, narration: line.Description);
            if (line.VatAmount > 0)
                AddJournalLine(entry.Id, request.VatAccountId!.Value, debit: line.VatAmount, narration: $"VAT on {line.Description}");
        }

        // CR Payable for the full invoice total
        AddJournalLine(entry.Id, payableAccountId, credit: totalAmount, narration: request.Description);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Purchase daybook entry {EntryId} created for organisation {OrganisationId}", entry.Id, organisationId);
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreateReceiptDaybookAsync(Guid organisationId, CreateReceiptDaybookRequest request)
    {
        _logger.LogDebug("Creating Receipt daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        await ValidateGLAccount(request.BankAccountId, "Bank");
        await ValidateGLAccount(request.CreditAccountId, "Credit");

        Guid? customerId = null;
        if (request.CustomerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
            if (customer == null)
                throw new ResourceNotFoundException("Customer", request.CustomerId.Value.ToString());
            customerId = customer.Id;
        }

        var entry = BuildDaybookEntry(organisationId, "Receipt", request.ReferenceNumber, request.EntryDate, request.Description, customerId, null);
        _context.DaybookEntries.Add(entry);

        // DR Bank, CR Credit account
        AddJournalLine(entry.Id, request.BankAccountId, debit: request.Amount, narration: request.Description);
        AddJournalLine(entry.Id, request.CreditAccountId, credit: request.Amount, narration: request.Description);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Receipt daybook entry {EntryId} created for organisation {OrganisationId}", entry.Id, organisationId);
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreatePaymentDaybookAsync(Guid organisationId, CreatePaymentDaybookRequest request)
    {
        _logger.LogDebug("Creating Payment daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        await ValidateGLAccount(request.DebitAccountId, "Debit");
        await ValidateGLAccount(request.BankAccountId, "Bank");

        Guid? supplierId = null;
        if (request.SupplierId.HasValue)
        {
            var supplier = await _context.Suppliers.FindAsync(request.SupplierId.Value);
            if (supplier == null)
                throw new ResourceNotFoundException("Supplier", request.SupplierId.Value.ToString());
            supplierId = supplier.Id;
        }

        var entry = BuildDaybookEntry(organisationId, "Payment", request.ReferenceNumber, request.EntryDate, request.Description, null, supplierId);
        _context.DaybookEntries.Add(entry);

        // DR Debit account (AP/Expense), CR Bank
        AddJournalLine(entry.Id, request.DebitAccountId, debit: request.Amount, narration: request.Description);
        AddJournalLine(entry.Id, request.BankAccountId, credit: request.Amount, narration: request.Description);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Payment daybook entry {EntryId} created for organisation {OrganisationId}", entry.Id, organisationId);
        return await MapToDaybookResponse(entry);
    }

    private async Task ValidateGLAccount(Guid accountId, string role)
    {
        var exists = await _context.GLAccounts.AnyAsync(a => a.Id == accountId && a.IsActive);
        if (!exists)
            throw new ResourceNotFoundException($"GL Account ({role})", accountId.ToString());
    }

    private DaybookEntry BuildDaybookEntry(Guid organisationId, string type, string? reference, DateTime entryDate, string? description, Guid? customerId, Guid? supplierId)
    {
        return new DaybookEntry
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Type = type,
            ReferenceNumber = reference,
            EntryDate = entryDate,
            Description = description,
            CustomerId = customerId,
            SupplierId = supplierId,
            IsPosted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private void AddJournalLine(Guid daybookEntryId, Guid glAccountId, decimal debit = 0, decimal credit = 0, string? narration = null)
    {
        _context.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            DaybookEntryId = daybookEntryId,
            GLAccountId = glAccountId,
            DebitAmount = debit,
            CreditAmount = credit,
            NarrationLine = narration
        });
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

        string? customerName = null;
        if (daybookEntry.CustomerId.HasValue)
            customerName = (await _context.Customers.FindAsync(daybookEntry.CustomerId.Value))?.Name;

        string? supplierName = null;
        if (daybookEntry.SupplierId.HasValue)
            supplierName = (await _context.Suppliers.FindAsync(daybookEntry.SupplierId.Value))?.Name;

        return new DaybookResponse
        {
            Id = daybookEntry.Id,
            Type = daybookEntry.Type,
            ReferenceNumber = daybookEntry.ReferenceNumber,
            EntryDate = daybookEntry.EntryDate,
            Description = daybookEntry.Description,
            IsPosted = daybookEntry.IsPosted,
            CustomerId = daybookEntry.CustomerId,
            CustomerName = customerName,
            SupplierId = daybookEntry.SupplierId,
            SupplierName = supplierName,
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
