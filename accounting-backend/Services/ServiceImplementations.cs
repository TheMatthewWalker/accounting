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
            .AnyAsync(m => m.OrganisationId == organisationId && m.User!.Email == request.InvitedEmail && m.IsActive);
        if (alreadyMember)
            throw new DuplicateResourceException($"A user with email '{request.InvitedEmail}' is already a member of this organisation.");

        var pendingInvite = await _context.OrganisationInvitations
            .AnyAsync(i => i.OrganisationId == organisationId && i.InvitedEmail == request.InvitedEmail
                        && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);
        if (pendingInvite)
            throw new DuplicateResourceException($"A pending invitation for '{request.InvitedEmail}' already exists.");

        var invitation = new OrganisationInvitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            InvitedEmail = request.InvitedEmail,
            Role = request.Role,
            Token = Guid.NewGuid().ToString("N"), // 32-char alphanumeric token
            InvitedByUserId = invitedByUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsAccepted = false
        };

        _context.OrganisationInvitations.Add(invitation);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Invitation created for {Email} to join organisation {OrganisationId} with role {Role}", request.InvitedEmail, organisationId, request.Role);

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

        await EnforceMonthlyEntryLimitAsync(organisationId);

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

        var internalRef = await NextInternalReferenceAsync(organisationId, request.Type);
        var daybookEntry = new DaybookEntry
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Type = request.Type,
            ReferenceNumber = internalRef,
            ExternalReference = request.ExternalReference,
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

    public async Task<DaybookResponse> UpdateJournalLineAccountAsync(Guid entryId, Guid lineId, Guid newGLAccountId)
    {
        var entry = await _context.DaybookEntries.FindAsync(entryId)
            ?? throw new ResourceNotFoundException("Daybook Entry", entryId.ToString());

        if (entry.IsPosted)
            throw new BusinessRuleException("Cannot modify a posted daybook entry.", "ENTRY_POSTED");

        var line = await _context.JournalEntries
            .FirstOrDefaultAsync(l => l.Id == lineId && l.DaybookEntryId == entryId)
            ?? throw new ResourceNotFoundException("Journal Line", lineId.ToString());

        var accountExists = await _context.GLAccounts
            .AnyAsync(a => a.Id == newGLAccountId && a.OrganisationId == entry.OrganisationId && a.IsActive);
        if (!accountExists)
            throw new ResourceNotFoundException("GL Account", newGLAccountId.ToString());

        line.GLAccountId = newGLAccountId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Journal line {LineId} on entry {EntryId} reassigned to GL account {AccountId}", lineId, entryId, newGLAccountId);
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreateSalesDaybookAsync(Guid organisationId, CreateSalesDaybookRequest request)
    {
        _logger.LogDebug("Creating Sales daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        Guid? customerId = null;
        Guid debitAccountId; // AR account (credit) or immediate payment asset account

        if (request.ImmediatePayment)
        {
            // Immediate payment: debit the chosen asset account directly — no AR involvement
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            debitAccountId = request.ImmediatePaymentAccountId.Value;

            // Customer is optional for record-keeping only
            if (request.CustomerId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                if (customer == null)
                    throw new ResourceNotFoundException("Customer", request.CustomerId.Value.ToString());
                customerId = customer.Id;
            }
        }
        else
        {
            // Credit sale: resolve the Accounts Receivable account
            if (request.CustomerId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                if (customer == null)
                    throw new ResourceNotFoundException("Customer", request.CustomerId.Value.ToString());
                customerId = customer.Id;
                if (customer.ControlAccountId.HasValue)
                    debitAccountId = customer.ControlAccountId.Value;
                else if (request.ReceivableAccountId.HasValue)
                    debitAccountId = request.ReceivableAccountId.Value;
                else
                    throw new ValidationException("The specified customer has no control account linked. Please provide a ReceivableAccountId.");
            }
            else if (request.ReceivableAccountId.HasValue)
            {
                debitAccountId = request.ReceivableAccountId.Value;
            }
            else
            {
                throw new ValidationException("Either CustomerId (with a linked AR account) or ReceivableAccountId must be specified.");
            }
            await ValidateGLAccount(debitAccountId, "Receivable");
        }

        bool hasVat = request.Lines.Any(l => l.VatAmount > 0);
        if (hasVat && !request.VatAccountId.HasValue)
            throw new ValidationException("VatAccountId is required when any line has a VAT amount.");
        if (request.VatAccountId.HasValue)
            await ValidateGLAccount(request.VatAccountId.Value, "VAT");

        foreach (var line in request.Lines)
            await ValidateGLAccount(line.RevenueAccountId, "Revenue");

        decimal totalAmount = request.Lines.Sum(l => l.NetAmount + l.VatAmount);

        var entry = await BuildDaybookEntryAsync(organisationId, "Sales", request.ExternalReference, request.EntryDate, request.Description, customerId, null);
        _context.DaybookEntries.Add(entry);

        // DR Receivable (credit) or payment asset account (immediate) for the full invoice total
        AddJournalLine(entry.Id, debitAccountId, debit: totalAmount, narration: request.Description);

        // CR Revenue (and VAT) per line
        foreach (var line in request.Lines)
        {
            AddJournalLine(entry.Id, line.RevenueAccountId, credit: line.NetAmount, narration: line.Description);
            if (line.VatAmount > 0)
                AddJournalLine(entry.Id, request.VatAccountId!.Value, credit: line.VatAmount, narration: $"VAT on {line.Description}");
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales daybook entry {EntryId} created for organisation {OrganisationId} (immediatePayment: {Immediate})", entry.Id, organisationId, request.ImmediatePayment);
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreateSalesReturnDaybookAsync(Guid organisationId, CreateSalesReturnDaybookRequest request)
    {
        _logger.LogDebug("Creating Sales Return (credit note) daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        Guid? customerId = null;
        Guid creditAccountId; // AR account (credit) or immediate payment asset account (cash/bank refunded)

        if (request.ImmediatePayment)
        {
            // Immediate refund: credit the chosen asset account directly — no AR involvement
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            creditAccountId = request.ImmediatePaymentAccountId.Value;

            if (request.CustomerId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                if (customer == null) throw new ResourceNotFoundException("Customer", request.CustomerId.Value.ToString());
                customerId = customer.Id;
            }
        }
        else
        {
            // Credit note: resolve the Accounts Receivable account
            if (request.CustomerId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                if (customer == null) throw new ResourceNotFoundException("Customer", request.CustomerId.Value.ToString());
                customerId = customer.Id;
                if (customer.ControlAccountId.HasValue)
                    creditAccountId = customer.ControlAccountId.Value;
                else if (request.ReceivableAccountId.HasValue)
                    creditAccountId = request.ReceivableAccountId.Value;
                else
                    throw new ValidationException("The specified customer has no control account linked. Please provide a ReceivableAccountId.");
            }
            else if (request.ReceivableAccountId.HasValue)
            {
                creditAccountId = request.ReceivableAccountId.Value;
            }
            else
            {
                throw new ValidationException("Either CustomerId (with a linked AR account) or ReceivableAccountId must be specified.");
            }
            await ValidateGLAccount(creditAccountId, "Receivable");
        }

        bool hasVat = request.Lines.Any(l => l.VatAmount > 0);
        if (hasVat && !request.VatAccountId.HasValue)
            throw new ValidationException("VatAccountId is required when any line has a VAT amount.");
        if (request.VatAccountId.HasValue)
            await ValidateGLAccount(request.VatAccountId.Value, "VAT");

        foreach (var line in request.Lines)
            await ValidateGLAccount(line.ExpenseAccountId, "Sales Returns Expense");

        decimal totalAmount = request.Lines.Sum(l => l.NetAmount + l.VatAmount);

        var entry = await BuildDaybookEntryAsync(organisationId, "SalesReturn", request.ExternalReference, request.EntryDate, request.Description, customerId, null);
        _context.DaybookEntries.Add(entry);

        // CR Receivable (reduce what customer owes) or CR asset account (immediate cash/bank refund)
        AddJournalLine(entry.Id, creditAccountId, credit: totalAmount, narration: request.Description);

        // DR Sales Returns Expense (and DR VAT) per line
        foreach (var line in request.Lines)
        {
            AddJournalLine(entry.Id, line.ExpenseAccountId, debit: line.NetAmount, narration: line.Description);
            if (line.VatAmount > 0)
                AddJournalLine(entry.Id, request.VatAccountId!.Value, debit: line.VatAmount, narration: $"VAT on {line.Description}");
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Sales Return daybook entry {EntryId} created for organisation {OrganisationId} (immediatePayment: {Immediate})", entry.Id, organisationId, request.ImmediatePayment);
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreatePurchaseDaybookAsync(Guid organisationId, CreatePurchaseDaybookRequest request)
    {
        _logger.LogDebug("Creating Purchase daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        Guid? supplierId = null;
        Guid creditAccountId; // AP account (credit) or immediate payment asset account (cash/bank paid out)

        if (request.ImmediatePayment)
        {
            // Immediate payment: credit the chosen asset account directly — no AP involvement
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            creditAccountId = request.ImmediatePaymentAccountId.Value;

            // Supplier is optional for record-keeping only
            if (request.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers.FindAsync(request.SupplierId.Value);
                if (supplier == null)
                    throw new ResourceNotFoundException("Supplier", request.SupplierId.Value.ToString());
                supplierId = supplier.Id;
            }
        }
        else
        {
            // Credit purchase: resolve the Accounts Payable account
            if (request.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers.FindAsync(request.SupplierId.Value);
                if (supplier == null)
                    throw new ResourceNotFoundException("Supplier", request.SupplierId.Value.ToString());
                supplierId = supplier.Id;
                if (supplier.ControlAccountId.HasValue)
                    creditAccountId = supplier.ControlAccountId.Value;
                else if (request.PayableAccountId.HasValue)
                    creditAccountId = request.PayableAccountId.Value;
                else
                    throw new ValidationException("The specified supplier has no control account linked. Please provide a PayableAccountId.");
            }
            else if (request.PayableAccountId.HasValue)
            {
                creditAccountId = request.PayableAccountId.Value;
            }
            else
            {
                throw new ValidationException("Either SupplierId (with a linked AP account) or PayableAccountId must be specified.");
            }
            await ValidateGLAccount(creditAccountId, "Payable");
        }

        bool hasVat = request.Lines.Any(l => l.VatAmount > 0);
        if (hasVat && !request.VatAccountId.HasValue)
            throw new ValidationException("VatAccountId is required when any line has a VAT amount.");
        if (request.VatAccountId.HasValue)
            await ValidateGLAccount(request.VatAccountId.Value, "VAT");

        foreach (var line in request.Lines)
            await ValidateGLAccount(line.ExpenseAccountId, "Expense");

        decimal totalAmount = request.Lines.Sum(l => l.NetAmount + l.VatAmount);

        var entry = await BuildDaybookEntryAsync(organisationId, "Purchase", request.ExternalReference, request.EntryDate, request.Description, null, supplierId);
        _context.DaybookEntries.Add(entry);

        // DR Expense (and VAT) per line
        foreach (var line in request.Lines)
        {
            AddJournalLine(entry.Id, line.ExpenseAccountId, debit: line.NetAmount, narration: line.Description);
            if (line.VatAmount > 0)
                AddJournalLine(entry.Id, request.VatAccountId!.Value, debit: line.VatAmount, narration: $"VAT on {line.Description}");
        }

        // CR Payable (credit) or CR asset account (immediate cash/bank payment)
        AddJournalLine(entry.Id, creditAccountId, credit: totalAmount, narration: request.Description);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Purchase daybook entry {EntryId} created for organisation {OrganisationId} (immediatePayment: {Immediate})", entry.Id, organisationId, request.ImmediatePayment);
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreatePurchaseReturnDaybookAsync(Guid organisationId, CreatePurchaseDaybookRequest request)
    {
        _logger.LogDebug("Creating Purchase Return daybook entry for organisation {OrganisationId}", organisationId);

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        Guid? supplierId = null;
        Guid debitAccountId; // AP account (debit) or immediate payment asset account (cash/bank received back)

        if (request.ImmediatePayment)
        {
            // Immediate refund: debit the chosen asset account directly — no AP involvement
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            debitAccountId = request.ImmediatePaymentAccountId.Value;

            if (request.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers.FindAsync(request.SupplierId.Value);
                if (supplier == null) throw new ResourceNotFoundException("Supplier", request.SupplierId.Value.ToString());
                supplierId = supplier.Id;
            }
        }
        else
        {
            // Credit purchase return: resolve the Accounts Payable account
            if (request.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers.FindAsync(request.SupplierId.Value);
                if (supplier == null) throw new ResourceNotFoundException("Supplier", request.SupplierId.Value.ToString());
                supplierId = supplier.Id;
                if (supplier.ControlAccountId.HasValue)
                    debitAccountId = supplier.ControlAccountId.Value;
                else if (request.PayableAccountId.HasValue)
                    debitAccountId = request.PayableAccountId.Value;
                else
                    throw new ValidationException("The specified supplier has no control account linked. Please provide a PayableAccountId.");
            }
            else if (request.PayableAccountId.HasValue)
            {
                debitAccountId = request.PayableAccountId.Value;
            }
            else
            {
                throw new ValidationException("Either SupplierId (with a linked AP account) or PayableAccountId must be specified.");
            }
            await ValidateGLAccount(debitAccountId, "Payable");
        }

        bool hasVat = request.Lines.Any(l => l.VatAmount > 0);
        if (hasVat && !request.VatAccountId.HasValue)
            throw new ValidationException("VatAccountId is required when any line has a VAT amount.");
        if (request.VatAccountId.HasValue)
            await ValidateGLAccount(request.VatAccountId.Value, "VAT");

        foreach (var line in request.Lines)
            await ValidateGLAccount(line.ExpenseAccountId, "Expense");

        decimal totalAmount = request.Lines.Sum(l => l.NetAmount + l.VatAmount);

        var entry = await BuildDaybookEntryAsync(organisationId, "PurchaseReturn", request.ExternalReference, request.EntryDate, request.Description, null, supplierId);
        _context.DaybookEntries.Add(entry);

        // DR Payable (reduce what we owe supplier) or DR asset account (immediate cash/bank refund received)
        AddJournalLine(entry.Id, debitAccountId, debit: totalAmount, narration: request.Description);

        // CR Expense (and VAT) per line — reversed
        foreach (var line in request.Lines)
        {
            AddJournalLine(entry.Id, line.ExpenseAccountId, credit: line.NetAmount, narration: line.Description);
            if (line.VatAmount > 0)
                AddJournalLine(entry.Id, request.VatAccountId!.Value, credit: line.VatAmount, narration: $"VAT on {line.Description}");
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Purchase Return daybook entry {EntryId} created for organisation {OrganisationId} (immediatePayment: {Immediate})", entry.Id, organisationId, request.ImmediatePayment);
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

        if (request.LinkedDaybookEntryId.HasValue)
        {
            var linkedExists = await _context.DaybookEntries
                .AnyAsync(e => e.Id == request.LinkedDaybookEntryId.Value && e.OrganisationId == organisationId);
            if (!linkedExists)
                throw new ResourceNotFoundException("Linked Daybook Entry", request.LinkedDaybookEntryId.Value.ToString());
        }

        var entry = await BuildDaybookEntryAsync(organisationId, "Receipt", request.ExternalReference, request.EntryDate, request.Description, customerId, null);
        entry.LinkedDaybookEntryId = request.LinkedDaybookEntryId;
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

        if (request.LinkedDaybookEntryId.HasValue)
        {
            var linkedExists = await _context.DaybookEntries
                .AnyAsync(e => e.Id == request.LinkedDaybookEntryId.Value && e.OrganisationId == organisationId);
            if (!linkedExists)
                throw new ResourceNotFoundException("Linked Daybook Entry", request.LinkedDaybookEntryId.Value.ToString());
        }

        var entry = await BuildDaybookEntryAsync(organisationId, "Payment", request.ExternalReference, request.EntryDate, request.Description, null, supplierId);
        entry.LinkedDaybookEntryId = request.LinkedDaybookEntryId;
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

    // ---- Simplified invoice methods (GL accounts auto-resolved) ----

    public async Task<DaybookResponse> CreateSimpleSalesDaybookAsync(Guid organisationId, SimpleInvoiceRequest request)
    {
        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        var (org, arAccountId, vatAccountId) = await ResolveOrgDefaults(organisationId, "1100");
        var lines = await ResolveSimpleLines(organisationId, request.Lines, org, "4000");
        decimal total = lines.Sum(l => l.netAmount + l.vatAmount);

        Guid debitAccountId;
        Guid? customerId;

        if (request.ImmediatePayment)
        {
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            debitAccountId = request.ImmediatePaymentAccountId.Value;
            (customerId, _) = await ResolveCustomer(request.CustomerId, arAccountId);
        }
        else
        {
            (customerId, debitAccountId) = await ResolveCustomer(request.CustomerId, arAccountId);
        }

        var entry = await BuildDaybookEntryAsync(organisationId, "Sales", request.ExternalReference, request.EntryDate, request.Description, customerId, null);
        _context.DaybookEntries.Add(entry);
        AddJournalLine(entry.Id, debitAccountId, debit: total, narration: request.Description);
        foreach (var (accountId, desc, netAmount, vatAmount) in lines)
        {
            AddJournalLine(entry.Id, accountId, credit: netAmount, narration: desc);
            if (vatAmount > 0 && vatAccountId.HasValue)
                AddJournalLine(entry.Id, vatAccountId.Value, credit: vatAmount, narration: $"VAT on {desc}");
        }
        await _context.SaveChangesAsync();
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreateSimpleSalesReturnDaybookAsync(Guid organisationId, SimpleInvoiceRequest request)
    {
        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        if (request.LinkedDaybookEntryId.HasValue)
        {
            var originalExists = await _context.DaybookEntries
                .AnyAsync(e => e.Id == request.LinkedDaybookEntryId.Value && e.OrganisationId == organisationId && e.Type == "Sales");
            if (!originalExists)
                throw new ResourceNotFoundException("Linked Sales Entry", request.LinkedDaybookEntryId.Value.ToString());
        }

        var (org, arAccountId, vatAccountId) = await ResolveOrgDefaults(organisationId, "1100");
        var lines = await ResolveSimpleLines(organisationId, request.Lines, org, "4100");
        decimal total = lines.Sum(l => l.netAmount + l.vatAmount);

        Guid creditAccountId;
        Guid? customerId;

        if (request.ImmediatePayment)
        {
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            creditAccountId = request.ImmediatePaymentAccountId.Value;
            (customerId, _) = await ResolveCustomer(request.CustomerId, arAccountId);
        }
        else
        {
            (customerId, creditAccountId) = await ResolveCustomer(request.CustomerId, arAccountId);
        }

        var entry = await BuildDaybookEntryAsync(organisationId, "SalesReturn", request.ExternalReference, request.EntryDate, request.Description, customerId, null);
        entry.LinkedDaybookEntryId = request.LinkedDaybookEntryId;
        _context.DaybookEntries.Add(entry);
        AddJournalLine(entry.Id, creditAccountId, credit: total, narration: request.Description);
        foreach (var (accountId, desc, netAmount, vatAmount) in lines)
        {
            AddJournalLine(entry.Id, accountId, debit: netAmount, narration: desc);
            if (vatAmount > 0 && vatAccountId.HasValue)
                AddJournalLine(entry.Id, vatAccountId.Value, debit: vatAmount, narration: $"VAT on {desc}");
        }
        await _context.SaveChangesAsync();
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreateSimplePurchaseDaybookAsync(Guid organisationId, SimpleInvoiceRequest request)
    {
        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        var (org, apAccountId, vatAccountId) = await ResolveOrgDefaults(organisationId, "2000");
        var lines = await ResolveSimpleLines(organisationId, request.Lines, org, "5000");
        decimal total = lines.Sum(l => l.netAmount + l.vatAmount);

        Guid creditAccountId;
        Guid? supplierId;

        if (request.ImmediatePayment)
        {
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            creditAccountId = request.ImmediatePaymentAccountId.Value;
            (supplierId, _) = await ResolveSupplier(request.SupplierId, apAccountId);
        }
        else
        {
            (supplierId, creditAccountId) = await ResolveSupplier(request.SupplierId, apAccountId);
        }

        var entry = await BuildDaybookEntryAsync(organisationId, "Purchase", request.ExternalReference, request.EntryDate, request.Description, null, supplierId);
        _context.DaybookEntries.Add(entry);
        foreach (var (accountId, desc, netAmount, vatAmount) in lines)
        {
            AddJournalLine(entry.Id, accountId, debit: netAmount, narration: desc);
            if (vatAmount > 0 && vatAccountId.HasValue)
                AddJournalLine(entry.Id, vatAccountId.Value, debit: vatAmount, narration: $"VAT on {desc}");
        }
        AddJournalLine(entry.Id, creditAccountId, credit: total, narration: request.Description);
        await _context.SaveChangesAsync();
        return await MapToDaybookResponse(entry);
    }

    public async Task<DaybookResponse> CreateSimplePurchaseReturnDaybookAsync(Guid organisationId, SimpleInvoiceRequest request)
    {
        if (request.EntryDate.Date > DateTime.UtcNow.Date)
            throw new ValidationException("Entry date cannot be in the future.");

        if (request.LinkedDaybookEntryId.HasValue)
        {
            var originalExists = await _context.DaybookEntries
                .AnyAsync(e => e.Id == request.LinkedDaybookEntryId.Value && e.OrganisationId == organisationId && e.Type == "Purchase");
            if (!originalExists)
                throw new ResourceNotFoundException("Linked Purchase Entry", request.LinkedDaybookEntryId.Value.ToString());
        }

        var (org, apAccountId, vatAccountId) = await ResolveOrgDefaults(organisationId, "2000");
        var lines = await ResolveSimpleLines(organisationId, request.Lines, org, "5100");
        decimal total = lines.Sum(l => l.netAmount + l.vatAmount);

        Guid debitAccountId;
        Guid? supplierId;

        if (request.ImmediatePayment)
        {
            if (!request.ImmediatePaymentAccountId.HasValue)
                throw new ValidationException("ImmediatePaymentAccountId is required when ImmediatePayment is true.");
            await ValidateGLAccount(request.ImmediatePaymentAccountId.Value, "Payment");
            debitAccountId = request.ImmediatePaymentAccountId.Value;
            (supplierId, _) = await ResolveSupplier(request.SupplierId, apAccountId);
        }
        else
        {
            (supplierId, debitAccountId) = await ResolveSupplier(request.SupplierId, apAccountId);
        }

        var entry = await BuildDaybookEntryAsync(organisationId, "PurchaseReturn", request.ExternalReference, request.EntryDate, request.Description, null, supplierId);
        entry.LinkedDaybookEntryId = request.LinkedDaybookEntryId;
        _context.DaybookEntries.Add(entry);
        AddJournalLine(entry.Id, debitAccountId, debit: total, narration: request.Description);
        foreach (var (accountId, desc, netAmount, vatAmount) in lines)
        {
            AddJournalLine(entry.Id, accountId, credit: netAmount, narration: desc);
            if (vatAmount > 0 && vatAccountId.HasValue)
                AddJournalLine(entry.Id, vatAccountId.Value, credit: vatAmount, narration: $"VAT on {desc}");
        }
        await _context.SaveChangesAsync();
        return await MapToDaybookResponse(entry);
    }

    // Resolve org defaults: returns (org, controlAccountId, vatAccountId?)
    private async Task<(Organisation org, Guid controlAccountId, Guid? vatAccountId)> ResolveOrgDefaults(Guid organisationId, string controlAccountCode)
    {
        var org = await _context.Organisations.FindAsync(organisationId)
            ?? throw new ResourceNotFoundException("Organisation", organisationId.ToString());

        var controlAccount = await _context.GLAccounts
            .FirstOrDefaultAsync(a => a.OrganisationId == organisationId && a.Code == controlAccountCode && a.IsActive)
            ?? throw new BusinessRuleException($"Default account '{controlAccountCode}' not found. Please set up your chart of accounts.", "MISSING_DEFAULT_ACCOUNT");

        return (org, controlAccount.Id, org.DefaultVatAccountId);
    }

    private async Task<(Guid? customerId, Guid arAccountId)> ResolveCustomer(Guid? customerId, Guid arAccountId)
    {
        if (!customerId.HasValue) return (null, arAccountId);
        var customer = await _context.Customers.FindAsync(customerId.Value)
            ?? throw new ResourceNotFoundException("Customer", customerId.Value.ToString());
        return (customer.Id, customer.ControlAccountId ?? arAccountId);
    }

    private async Task<(Guid? supplierId, Guid apAccountId)> ResolveSupplier(Guid? supplierId, Guid apAccountId)
    {
        if (!supplierId.HasValue) return (null, apAccountId);
        var supplier = await _context.Suppliers.FindAsync(supplierId.Value)
            ?? throw new ResourceNotFoundException("Supplier", supplierId.Value.ToString());
        return (supplier.Id, supplier.ControlAccountId ?? apAccountId);
    }

    private async Task<List<(Guid accountId, string desc, decimal netAmount, decimal vatAmount)>> ResolveSimpleLines(
        Guid organisationId, List<SimpleInvoiceLine> lines, Organisation org, string defaultAccountCode)
    {
        var result = new List<(Guid, string, decimal, decimal)>();
        var defaultAccount = await _context.GLAccounts
            .FirstOrDefaultAsync(a => a.OrganisationId == organisationId && a.Code == defaultAccountCode && a.IsActive)
            ?? throw new BusinessRuleException($"Default account '{defaultAccountCode}' not found. Please set up your chart of accounts.", "MISSING_DEFAULT_ACCOUNT");

        foreach (var line in lines)
        {
            var accountId = defaultAccount.Id;
            var description = line.Description ?? string.Empty;
            var vatTreatment = line.VatTreatment;

            if (line.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(line.ProductId.Value);
                if (product != null)
                {
                    if (string.IsNullOrWhiteSpace(description))
                        description = product.Name;
                    if (vatTreatment == "standard") // only override if not explicitly changed
                        vatTreatment = product.VatTreatment;
                }
            }

            var vatRate = vatTreatment switch
            {
                "standard" => org.VatFullRate,
                "reduced"  => org.VatReducedRate,
                _ => 0m
            };

            var netAmount = Math.Round(line.Quantity * line.UnitPrice, 2);
            var vatAmount = vatTreatment is "exempt" or "zero" ? 0m : Math.Round(netAmount * vatRate / 100, 2);
            result.Add((accountId, description, netAmount, vatAmount));
        }
        return result;
    }

    private static readonly Dictionary<string, string> _typePrefixes = new()
    {
        ["Sales"]          = "SI",
        ["SalesReturn"]    = "SR",
        ["Purchase"]       = "PI",
        ["PurchaseReturn"] = "PR",
        ["Receipt"]        = "RC",
        ["Payment"]        = "PY",
        ["Journal"]        = "JN",
    };

    private async Task EnforceMonthlyEntryLimitAsync(Guid organisationId)
    {
        var org = await _context.Organisations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organisationId);
        if (org == null)
            throw new ResourceNotFoundException("Organisation", organisationId.ToString());

        int? limit = org.SubscriptionTier switch
        {
            "Enterprise" => null,
            "Pro"        => 1000,
            _            => 50,   // Free
        };

        if (limit == null)
            return;

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var count = await _context.DaybookEntries
            .CountAsync(e => e.OrganisationId == organisationId
                          && e.CreatedAt >= monthStart
                          && e.CreatedAt < monthEnd);

        if (count >= limit)
            throw new BusinessRuleException(
                $"{org.SubscriptionTier} plan organisations are limited to {limit} daybook entries per month. " +
                (org.SubscriptionTier == "Free"
                    ? "Upgrade to Pro (1,000/month) or Enterprise (unlimited) to continue."
                    : "Upgrade to Enterprise for unlimited entries."));
    }

    private async Task<string> NextInternalReferenceAsync(Guid organisationId, string entryType)
    {
        var prefix = _typePrefixes.GetValueOrDefault(entryType, "XX");
        var seq = await _context.DaybookSequences
            .FirstOrDefaultAsync(s => s.OrganisationId == organisationId && s.EntryType == entryType);
        if (seq == null)
        {
            seq = new DaybookSequence { OrganisationId = organisationId, EntryType = entryType, LastNumber = 0 };
            _context.DaybookSequences.Add(seq);
        }
        seq.LastNumber++;
        return $"{prefix}-{seq.LastNumber:D8}";
    }

    private async Task<DaybookEntry> BuildDaybookEntryAsync(Guid organisationId, string type, string? externalReference, DateTime entryDate, string? description, Guid? customerId, Guid? supplierId)
    {
        await EnforceMonthlyEntryLimitAsync(organisationId);
        var internalRef = await NextInternalReferenceAsync(organisationId, type);
        return new DaybookEntry
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Type = type,
            ReferenceNumber = internalRef,
            ExternalReference = externalReference,
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

        string? linkedReference = null;
        if (daybookEntry.LinkedDaybookEntryId.HasValue)
            linkedReference = (await _context.DaybookEntries.FindAsync(daybookEntry.LinkedDaybookEntryId.Value))?.ReferenceNumber;

        return new DaybookResponse
        {
            Id = daybookEntry.Id,
            Type = daybookEntry.Type,
            ReferenceNumber = daybookEntry.ReferenceNumber,
            ExternalReference = daybookEntry.ExternalReference,
            EntryDate = daybookEntry.EntryDate,
            Description = daybookEntry.Description,
            IsPosted = daybookEntry.IsPosted,
            CustomerId = daybookEntry.CustomerId,
            CustomerName = customerName,
            SupplierId = daybookEntry.SupplierId,
            SupplierName = supplierName,
            LinkedDaybookEntryId = daybookEntry.LinkedDaybookEntryId,
            LinkedReferenceNumber = linkedReference,
            Lines = lines
        };
    }
}

public class ProductCatalogueService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductCatalogueService> _logger;

    public ProductCatalogueService(ApplicationDbContext context, ILogger<ProductCatalogueService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductServiceResponse> CreateAsync(Guid organisationId, CreateProductServiceRequest request)
    {
        var product = new ProductService
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            DefaultSalePrice = request.DefaultSalePrice,
            DefaultPurchasePrice = request.DefaultPurchasePrice,
            VatTreatment = request.VatTreatment,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Product {ProductId} created for organisation {OrgId}", product.Id, organisationId);
        return Map(product);
    }

    public async Task<ProductServiceResponse> GetAsync(Guid productId)
    {
        var product = await _context.Products.FindAsync(productId)
            ?? throw new ResourceNotFoundException("Product", productId.ToString());
        return Map(product);
    }

    public async Task<IEnumerable<ProductServiceResponse>> GetByOrganisationAsync(Guid organisationId)
    {
        var products = await _context.Products
            .Where(p => p.OrganisationId == organisationId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return products.Select(Map);
    }

    public async Task<ProductServiceResponse> UpdateAsync(Guid productId, UpdateProductServiceRequest request)
    {
        var product = await _context.Products.FindAsync(productId)
            ?? throw new ResourceNotFoundException("Product", productId.ToString());
        product.Name = request.Name;
        product.Code = request.Code;
        product.Description = request.Description;
        product.DefaultSalePrice = request.DefaultSalePrice;
        product.DefaultPurchasePrice = request.DefaultPurchasePrice;
        product.VatTreatment = request.VatTreatment;
        product.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return Map(product);
    }

    public async Task DeleteAsync(Guid productId)
    {
        var product = await _context.Products.FindAsync(productId)
            ?? throw new ResourceNotFoundException("Product", productId.ToString());
        product.IsActive = false;
        await _context.SaveChangesAsync();
    }

    private static ProductServiceResponse Map(ProductService p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Code = p.Code,
        Description = p.Description,
        DefaultSalePrice = p.DefaultSalePrice,
        DefaultPurchasePrice = p.DefaultPurchasePrice,
        VatTreatment = p.VatTreatment,
        IsActive = p.IsActive
    };
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

    // ── Pro Tier Reports ──────────────────────────────────────────────────────

    public async Task<AgedDebtorsResponse> GetAgedDebtorsAsync(Guid organisationId, DateTime asOfDate)
    {
        _logger.LogInformation("Generating aged debtors for organisation {OrganisationId} as of {AsOfDate}", organisationId, asOfDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier == "Free")
            throw new ForbiddenException("Aged Debtors report requires a Pro or Enterprise subscription.");

        var customers = await _context.Customers
            .Where(c => c.OrganisationId == organisationId && c.IsActive && c.ControlAccountId != null)
            .ToListAsync();

        var response = new AgedDebtorsResponse { AsOfDate = asOfDate };

        foreach (var customer in customers)
        {
            var salesEntries = await _context.DaybookEntries
                .Where(de => de.OrganisationId == organisationId &&
                             de.CustomerId == customer.Id &&
                             de.Type == "Sales" &&
                             de.IsPosted &&
                             de.EntryDate <= asOfDate)
                .ToListAsync();

            if (!salesEntries.Any()) continue;

            var customerLine = new AgedDebtorLine
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name
            };

            foreach (var invoice in salesEntries)
            {
                var invoiceDebit = await _context.JournalEntries
                    .Where(je => je.DaybookEntryId == invoice.Id && je.GLAccountId == customer.ControlAccountId)
                    .SumAsync(je => je.DebitAmount);

                var linkedReceipts = await _context.DaybookEntries
                    .Where(de => de.LinkedDaybookEntryId == invoice.Id && de.Type == "Receipt" && de.IsPosted && de.EntryDate <= asOfDate)
                    .ToListAsync();

                decimal totalSettled = 0;
                foreach (var receipt in linkedReceipts)
                {
                    totalSettled += await _context.JournalEntries
                        .Where(je => je.DaybookEntryId == receipt.Id && je.GLAccountId == customer.ControlAccountId)
                        .SumAsync(je => je.CreditAmount);
                }

                decimal outstanding = invoiceDebit - totalSettled;
                if (outstanding <= 0.01m) continue;

                int ageDays = (int)(asOfDate - invoice.EntryDate).TotalDays;
                if (ageDays <= 30) customerLine.Current += outstanding;
                else if (ageDays <= 60) customerLine.Days30 += outstanding;
                else if (ageDays <= 90) customerLine.Days60 += outstanding;
                else customerLine.Over90 += outstanding;
            }

            customerLine.Total = customerLine.Current + customerLine.Days30 + customerLine.Days60 + customerLine.Over90;
            if (customerLine.Total > 0.01m) response.Customers.Add(customerLine);
        }

        response.Customers = response.Customers.OrderByDescending(c => c.Total).ToList();
        response.TotalCurrent = response.Customers.Sum(c => c.Current);
        response.Total30Days = response.Customers.Sum(c => c.Days30);
        response.Total60Days = response.Customers.Sum(c => c.Days60);
        response.TotalOver90Days = response.Customers.Sum(c => c.Over90);
        response.GrandTotal = response.Customers.Sum(c => c.Total);

        return response;
    }

    public async Task<AgedCreditorsResponse> GetAgedCreditorsAsync(Guid organisationId, DateTime asOfDate)
    {
        _logger.LogInformation("Generating aged creditors for organisation {OrganisationId} as of {AsOfDate}", organisationId, asOfDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier == "Free")
            throw new ForbiddenException("Aged Creditors report requires a Pro or Enterprise subscription.");

        var suppliers = await _context.Suppliers
            .Where(s => s.OrganisationId == organisationId && s.IsActive && s.ControlAccountId != null)
            .ToListAsync();

        var response = new AgedCreditorsResponse { AsOfDate = asOfDate };

        foreach (var supplier in suppliers)
        {
            var purchaseEntries = await _context.DaybookEntries
                .Where(de => de.OrganisationId == organisationId &&
                             de.SupplierId == supplier.Id &&
                             de.Type == "Purchase" &&
                             de.IsPosted &&
                             de.EntryDate <= asOfDate)
                .ToListAsync();

            if (!purchaseEntries.Any()) continue;

            var supplierLine = new AgedCreditorLine
            {
                SupplierId = supplier.Id,
                SupplierName = supplier.Name
            };

            foreach (var bill in purchaseEntries)
            {
                var billCredit = await _context.JournalEntries
                    .Where(je => je.DaybookEntryId == bill.Id && je.GLAccountId == supplier.ControlAccountId)
                    .SumAsync(je => je.CreditAmount);

                var linkedPayments = await _context.DaybookEntries
                    .Where(de => de.LinkedDaybookEntryId == bill.Id && de.Type == "Payment" && de.IsPosted && de.EntryDate <= asOfDate)
                    .ToListAsync();

                decimal totalPaid = 0;
                foreach (var payment in linkedPayments)
                {
                    totalPaid += await _context.JournalEntries
                        .Where(je => je.DaybookEntryId == payment.Id && je.GLAccountId == supplier.ControlAccountId)
                        .SumAsync(je => je.DebitAmount);
                }

                decimal outstanding = billCredit - totalPaid;
                if (outstanding <= 0.01m) continue;

                int ageDays = (int)(asOfDate - bill.EntryDate).TotalDays;
                if (ageDays <= 30) supplierLine.Current += outstanding;
                else if (ageDays <= 60) supplierLine.Days30 += outstanding;
                else if (ageDays <= 90) supplierLine.Days60 += outstanding;
                else supplierLine.Over90 += outstanding;
            }

            supplierLine.Total = supplierLine.Current + supplierLine.Days30 + supplierLine.Days60 + supplierLine.Over90;
            if (supplierLine.Total > 0.01m) response.Suppliers.Add(supplierLine);
        }

        response.Suppliers = response.Suppliers.OrderByDescending(s => s.Total).ToList();
        response.TotalCurrent = response.Suppliers.Sum(s => s.Current);
        response.Total30Days = response.Suppliers.Sum(s => s.Days30);
        response.Total60Days = response.Suppliers.Sum(s => s.Days60);
        response.TotalOver90Days = response.Suppliers.Sum(s => s.Over90);
        response.GrandTotal = response.Suppliers.Sum(s => s.Total);

        return response;
    }

    public async Task<VatReturnResponse> GetVatReturnAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating VAT return for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier == "Free")
            throw new ForbiddenException("VAT Return report requires a Pro or Enterprise subscription.");

        if (toDate < fromDate)
            throw new ValidationException("toDate must be greater than or equal to fromDate.");

        var vatAccounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive &&
                        a.SubType != null && (a.SubType.Contains("VAT") || a.SubType.Contains("Tax")))
            .ToListAsync();

        if (org.DefaultVatAccountId != null && !vatAccounts.Any(a => a.Id == org.DefaultVatAccountId))
        {
            var defaultVat = await _context.GLAccounts.FindAsync(org.DefaultVatAccountId);
            if (defaultVat != null) vatAccounts.Add(defaultVat);
        }

        var response = new VatReturnResponse { FromDate = fromDate, ToDate = toDate };

        foreach (var account in vatAccounts)
        {
            var outputVat = await _context.JournalEntries
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.IsPosted &&
                             je.DaybookEntry!.EntryDate >= fromDate &&
                             je.DaybookEntry!.EntryDate <= toDate &&
                             (je.DaybookEntry!.Type == "Sales" || je.DaybookEntry!.Type == "SalesReturn"))
                .SumAsync(je => je.CreditAmount - je.DebitAmount);

            var inputVat = await _context.JournalEntries
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.IsPosted &&
                             je.DaybookEntry!.EntryDate >= fromDate &&
                             je.DaybookEntry!.EntryDate <= toDate &&
                             (je.DaybookEntry!.Type == "Purchase" || je.DaybookEntry!.Type == "PurchaseReturn"))
                .SumAsync(je => je.DebitAmount - je.CreditAmount);

            if (Math.Abs(outputVat) > 0.01m || Math.Abs(inputVat) > 0.01m)
            {
                response.Lines.Add(new VatReturnLine
                {
                    AccountCode = account.Code,
                    AccountName = account.Name,
                    OutputVat = outputVat,
                    InputVat = inputVat
                });
            }
        }

        response.OutputVat = response.Lines.Sum(l => l.OutputVat);
        response.InputVat = response.Lines.Sum(l => l.InputVat);
        response.NetVatPayable = response.OutputVat - response.InputVat;

        return response;
    }

    public async Task<IncomeByCustomerResponse> GetIncomeByCustomerAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating income by customer for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier == "Free")
            throw new ForbiddenException("Income by Customer report requires a Pro or Enterprise subscription.");

        if (toDate < fromDate)
            throw new ValidationException("toDate must be greater than or equal to fromDate.");

        var customers = await _context.Customers
            .Where(c => c.OrganisationId == organisationId && c.IsActive && c.ControlAccountId != null)
            .ToListAsync();

        var response = new IncomeByCustomerResponse { FromDate = fromDate, ToDate = toDate };

        foreach (var customer in customers)
        {
            var salesEntries = await _context.DaybookEntries
                .Where(de => de.OrganisationId == organisationId &&
                             de.CustomerId == customer.Id &&
                             (de.Type == "Sales" || de.Type == "SalesReturn") &&
                             de.IsPosted &&
                             de.EntryDate >= fromDate &&
                             de.EntryDate <= toDate)
                .ToListAsync();

            if (!salesEntries.Any()) continue;

            decimal totalInvoiced = 0;
            foreach (var entry in salesEntries)
            {
                var net = await _context.JournalEntries
                    .Where(je => je.DaybookEntryId == entry.Id && je.GLAccountId == customer.ControlAccountId)
                    .SumAsync(je => je.DebitAmount - je.CreditAmount);
                totalInvoiced += net;
            }

            var receiptEntries = await _context.DaybookEntries
                .Where(de => de.OrganisationId == organisationId &&
                             de.CustomerId == customer.Id &&
                             de.Type == "Receipt" &&
                             de.IsPosted &&
                             de.EntryDate >= fromDate &&
                             de.EntryDate <= toDate)
                .ToListAsync();

            decimal totalReceived = 0;
            foreach (var receipt in receiptEntries)
            {
                var credit = await _context.JournalEntries
                    .Where(je => je.DaybookEntryId == receipt.Id && je.GLAccountId == customer.ControlAccountId)
                    .SumAsync(je => je.CreditAmount - je.DebitAmount);
                totalReceived += credit;
            }

            response.Lines.Add(new IncomeByCustomerLine
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                InvoiceCount = salesEntries.Count,
                TotalInvoiced = totalInvoiced,
                TotalReceived = totalReceived,
                Outstanding = totalInvoiced - totalReceived
            });
        }

        response.Lines = response.Lines.OrderByDescending(l => l.TotalInvoiced).ToList();
        response.Total = response.Lines.Sum(l => l.TotalInvoiced);

        return response;
    }

    public async Task<SpendBySupplierResponse> GetSpendBySupplierAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating spend by supplier for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier == "Free")
            throw new ForbiddenException("Spend by Supplier report requires a Pro or Enterprise subscription.");

        if (toDate < fromDate)
            throw new ValidationException("toDate must be greater than or equal to fromDate.");

        var suppliers = await _context.Suppliers
            .Where(s => s.OrganisationId == organisationId && s.IsActive && s.ControlAccountId != null)
            .ToListAsync();

        var response = new SpendBySupplierResponse { FromDate = fromDate, ToDate = toDate };

        foreach (var supplier in suppliers)
        {
            var purchaseEntries = await _context.DaybookEntries
                .Where(de => de.OrganisationId == organisationId &&
                             de.SupplierId == supplier.Id &&
                             (de.Type == "Purchase" || de.Type == "PurchaseReturn") &&
                             de.IsPosted &&
                             de.EntryDate >= fromDate &&
                             de.EntryDate <= toDate)
                .ToListAsync();

            if (!purchaseEntries.Any()) continue;

            decimal totalInvoiced = 0;
            foreach (var entry in purchaseEntries)
            {
                var net = await _context.JournalEntries
                    .Where(je => je.DaybookEntryId == entry.Id && je.GLAccountId == supplier.ControlAccountId)
                    .SumAsync(je => je.CreditAmount - je.DebitAmount);
                totalInvoiced += net;
            }

            var paymentEntries = await _context.DaybookEntries
                .Where(de => de.OrganisationId == organisationId &&
                             de.SupplierId == supplier.Id &&
                             de.Type == "Payment" &&
                             de.IsPosted &&
                             de.EntryDate >= fromDate &&
                             de.EntryDate <= toDate)
                .ToListAsync();

            decimal totalPaid = 0;
            foreach (var payment in paymentEntries)
            {
                var debit = await _context.JournalEntries
                    .Where(je => je.DaybookEntryId == payment.Id && je.GLAccountId == supplier.ControlAccountId)
                    .SumAsync(je => je.DebitAmount - je.CreditAmount);
                totalPaid += debit;
            }

            response.Lines.Add(new SpendBySupplierLine
            {
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                InvoiceCount = purchaseEntries.Count,
                TotalInvoiced = totalInvoiced,
                TotalPaid = totalPaid,
                Outstanding = totalInvoiced - totalPaid
            });
        }

        response.Lines = response.Lines.OrderByDescending(l => l.TotalInvoiced).ToList();
        response.Total = response.Lines.Sum(l => l.TotalInvoiced);

        return response;
    }

    // ── Enterprise Tier Reports ───────────────────────────────────────────────

    public async Task<ComparativeProfitAndLossResponse> GetComparativeProfitAndLossAsync(
        Guid organisationId, DateTime period1From, DateTime period1To, DateTime period2From, DateTime period2To)
    {
        _logger.LogInformation("Generating comparative P&L for organisation {OrganisationId}", organisationId);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier != "Enterprise")
            throw new ForbiddenException("Comparative P&L report requires an Enterprise subscription.");

        var p1 = await GetProfitAndLossAsync(organisationId, period1From, period1To);
        var p2 = await GetProfitAndLossAsync(organisationId, period2From, period2To);

        return new ComparativeProfitAndLossResponse
        {
            Period1From = period1From,
            Period1To = period1To,
            Period2From = period2From,
            Period2To = period2To,
            Revenue = MergeComparativeLines(p1.Revenue, p2.Revenue),
            CostOfSales = MergeComparativeLines(p1.CostOfSales, p2.CostOfSales),
            OperatingExpenses = MergeComparativeLines(p1.OperatingExpenses, p2.OperatingExpenses),
            FinanceCosts = MergeComparativeLines(p1.FinanceCosts, p2.FinanceCosts),
            GrossProfit1 = p1.GrossProfit,
            GrossProfit2 = p2.GrossProfit,
            NetProfit1 = p1.NetProfit,
            NetProfit2 = p2.NetProfit
        };
    }

    private static List<ComparativePnLLine> MergeComparativeLines(List<PnLLineResponse> p1Lines, List<PnLLineResponse> p2Lines)
    {
        var allCodes = p1Lines.Select(l => l.Code).Concat(p2Lines.Select(l => l.Code)).Distinct();
        return allCodes.Select(code =>
        {
            var l1 = p1Lines.FirstOrDefault(l => l.Code == code);
            var l2 = p2Lines.FirstOrDefault(l => l.Code == code);
            decimal period1 = l1?.Amount ?? 0;
            decimal period2 = l2?.Amount ?? 0;
            return new ComparativePnLLine
            {
                Code = code,
                Name = (l1 ?? l2)!.Name,
                SubType = (l1 ?? l2)!.SubType,
                Period1 = period1,
                Period2 = period2,
                Variance = period1 - period2
            };
        }).OrderBy(l => l.Code).ToList();
    }

    public async Task<CashFlowStatementResponse> GetCashFlowStatementAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating cash flow statement for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier != "Enterprise")
            throw new ForbiddenException("Cash Flow Statement requires an Enterprise subscription.");

        if (toDate < fromDate)
            throw new ValidationException("toDate must be greater than or equal to fromDate.");

        // Identify bank/cash accounts
        var cashAccounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive && a.Type == "Asset" &&
                        a.SubType != null && (a.SubType.Contains("Bank") || a.SubType.Contains("Cash")))
            .ToListAsync();

        var response = new CashFlowStatementResponse { FromDate = fromDate, ToDate = toDate };

        // Opening cash: sum of opening balances + all movements before fromDate
        foreach (var cashAccount in cashAccounts)
        {
            decimal openingMovement = await _context.JournalEntries
                .Where(je => je.GLAccountId == cashAccount.Id &&
                             je.DaybookEntry!.IsPosted &&
                             je.DaybookEntry!.EntryDate < fromDate)
                .SumAsync(je => je.DebitAmount - je.CreditAmount);
            response.OpeningCash += cashAccount.OpeningBalance + openingMovement;
        }

        // Categorise cash movements in the period
        var periodEntries = await _context.DaybookEntries
            .Where(de => de.OrganisationId == organisationId &&
                         de.IsPosted &&
                         de.EntryDate >= fromDate &&
                         de.EntryDate <= toDate)
            .ToListAsync();

        decimal cashFromCustomers = 0, cashToSuppliers = 0, otherOperating = 0;
        decimal investing = 0, financing = 0;

        var fixedAssetAccountIds = (await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive && a.Type == "Asset" &&
                        a.SubType != null && a.SubType.Contains("Non"))
            .Select(a => a.Id).ToListAsync()).ToHashSet();

        var equityAccountIds = (await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive && a.Type == "Equity")
            .Select(a => a.Id).ToListAsync()).ToHashSet();

        var cashAccountIds = cashAccounts.Select(a => a.Id).ToHashSet();

        foreach (var entry in periodEntries)
        {
            var lines = await _context.JournalEntries
                .Where(je => je.DaybookEntryId == entry.Id && cashAccountIds.Contains(je.GLAccountId))
                .ToListAsync();

            decimal cashNet = lines.Sum(je => je.DebitAmount - je.CreditAmount);
            if (Math.Abs(cashNet) <= 0.01m) continue;

            bool hasFixedAsset = await _context.JournalEntries
                .AnyAsync(je => je.DaybookEntryId == entry.Id && fixedAssetAccountIds.Contains(je.GLAccountId));

            bool hasEquity = await _context.JournalEntries
                .AnyAsync(je => je.DaybookEntryId == entry.Id && equityAccountIds.Contains(je.GLAccountId));

            if (hasFixedAsset)
                investing += cashNet;
            else if (hasEquity)
                financing += cashNet;
            else if (entry.Type == "Receipt")
                cashFromCustomers += cashNet;
            else if (entry.Type == "Payment")
                cashToSuppliers += cashNet;
            else
                otherOperating += cashNet;
        }

        if (Math.Abs(cashFromCustomers) > 0.01m)
            response.OperatingActivities.Add(new CashFlowLine { Description = "Cash received from customers", Amount = cashFromCustomers });
        if (Math.Abs(cashToSuppliers) > 0.01m)
            response.OperatingActivities.Add(new CashFlowLine { Description = "Cash paid to suppliers", Amount = cashToSuppliers });
        if (Math.Abs(otherOperating) > 0.01m)
            response.OperatingActivities.Add(new CashFlowLine { Description = "Other operating cash flows", Amount = otherOperating });
        if (Math.Abs(investing) > 0.01m)
            response.InvestingActivities.Add(new CashFlowLine { Description = "Net investing activities", Amount = investing });
        if (Math.Abs(financing) > 0.01m)
            response.FinancingActivities.Add(new CashFlowLine { Description = "Net financing activities", Amount = financing });

        response.NetOperatingCashFlow = response.OperatingActivities.Sum(l => l.Amount);
        response.NetInvestingCashFlow = response.InvestingActivities.Sum(l => l.Amount);
        response.NetFinancingCashFlow = response.FinancingActivities.Sum(l => l.Amount);
        response.NetCashFlow = response.NetOperatingCashFlow + response.NetInvestingCashFlow + response.NetFinancingCashFlow;
        response.ClosingCash = response.OpeningCash + response.NetCashFlow;

        return response;
    }

    public async Task<AccountActivitySummaryResponse> GetAccountActivitySummaryAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating account activity summary for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier != "Enterprise")
            throw new ForbiddenException("Account Activity Summary requires an Enterprise subscription.");

        if (toDate < fromDate)
            throw new ValidationException("toDate must be greater than or equal to fromDate.");

        var accounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync();

        var response = new AccountActivitySummaryResponse { FromDate = fromDate, ToDate = toDate };

        // Build list of year-month pairs spanning the range
        var months = new List<(int Year, int Month)>();
        var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
        var end = new DateTime(toDate.Year, toDate.Month, 1);
        while (cursor <= end)
        {
            months.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        foreach (var account in accounts)
        {
            var allEntries = await _context.JournalEntries
                .AsNoTracking()
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.IsPosted &&
                             je.DaybookEntry!.EntryDate >= fromDate &&
                             je.DaybookEntry!.EntryDate <= toDate)
                .Select(je => new { je.DebitAmount, je.CreditAmount, je.DaybookEntry!.EntryDate })
                .ToListAsync();

            if (!allEntries.Any()) continue;

            var accountLine = new AccountActivityLine
            {
                Code = account.Code,
                Name = account.Name,
                Type = account.Type,
                TotalDebits = allEntries.Sum(e => e.DebitAmount),
                TotalCredits = allEntries.Sum(e => e.CreditAmount)
            };
            accountLine.NetMovement = accountLine.TotalDebits - accountLine.TotalCredits;

            foreach (var (year, month) in months)
            {
                var monthEntries = allEntries.Where(e => e.EntryDate.Year == year && e.EntryDate.Month == month).ToList();
                decimal debits = monthEntries.Sum(e => e.DebitAmount);
                decimal credits = monthEntries.Sum(e => e.CreditAmount);
                accountLine.Months.Add(new MonthlyActivityLine
                {
                    Year = year,
                    Month = month,
                    MonthName = new DateTime(year, month, 1).ToString("MMM yyyy"),
                    Debits = debits,
                    Credits = credits,
                    Net = debits - credits
                });
            }

            response.Accounts.Add(accountLine);
        }

        return response;
    }

    public async Task<RevenueBreakdownResponse> GetRevenueBreakdownAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating revenue breakdown for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier != "Enterprise")
            throw new ForbiddenException("Revenue Breakdown report requires an Enterprise subscription.");

        if (toDate < fromDate)
            throw new ValidationException("toDate must be greater than or equal to fromDate.");

        var revenueAccounts = await _context.GLAccounts
            .Where(a => a.OrganisationId == organisationId && a.IsActive && a.Type == "Revenue")
            .OrderBy(a => a.Code)
            .ToListAsync();

        var response = new RevenueBreakdownResponse { FromDate = fromDate, ToDate = toDate };

        foreach (var account in revenueAccounts)
        {
            var salesLines = await _context.JournalEntries
                .AsNoTracking()
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.IsPosted &&
                             je.DaybookEntry!.EntryDate >= fromDate &&
                             je.DaybookEntry!.EntryDate <= toDate &&
                             (je.DaybookEntry!.Type == "Sales" || je.DaybookEntry!.Type == "SalesReturn"))
                .Select(je => new { je.CreditAmount, je.DebitAmount, je.DaybookEntry!.Type, je.DaybookEntryId })
                .ToListAsync();

            var otherLines = await _context.JournalEntries
                .AsNoTracking()
                .Where(je => je.GLAccountId == account.Id &&
                             je.DaybookEntry!.IsPosted &&
                             je.DaybookEntry!.EntryDate >= fromDate &&
                             je.DaybookEntry!.EntryDate <= toDate &&
                             je.DaybookEntry!.Type != "Sales" &&
                             je.DaybookEntry!.Type != "SalesReturn")
                .Select(je => new { je.CreditAmount, je.DebitAmount, je.DaybookEntry!.Type, je.DaybookEntryId })
                .ToListAsync();

            var allLines = salesLines.Concat(otherLines).ToList();
            if (!allLines.Any()) continue;

            decimal salesAmount = salesLines
                .Where(l => l.Type == "Sales")
                .Sum(l => l.CreditAmount - l.DebitAmount);
            decimal returnsAmount = salesLines
                .Where(l => l.Type == "SalesReturn")
                .Sum(l => l.DebitAmount - l.CreditAmount);
            decimal otherAmount = otherLines.Sum(l => l.CreditAmount - l.DebitAmount);
            decimal netAmount = salesAmount - returnsAmount + otherAmount;

            if (Math.Abs(netAmount) <= 0.01m && salesAmount == 0) continue;

            int txCount = allLines.Select(l => l.DaybookEntryId).Distinct().Count();

            response.Lines.Add(new RevenueBreakdownLine
            {
                Code = account.Code,
                Name = account.Name,
                SubType = account.SubType ?? "",
                TransactionCount = txCount,
                SalesAmount = salesAmount,
                ReturnsAmount = returnsAmount,
                NetAmount = netAmount
            });
        }

        response.Lines = response.Lines.OrderByDescending(l => l.NetAmount).ToList();
        response.TotalRevenue = response.Lines.Sum(l => l.NetAmount);

        return response;
    }

    public async Task<DaybookAuditResponse> GetDaybookAuditAsync(Guid organisationId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Generating daybook audit for organisation {OrganisationId} from {FromDate} to {ToDate}", organisationId, fromDate, toDate);

        var org = await _context.Organisations.FindAsync(organisationId);
        if (org == null || org.SubscriptionTier != "Enterprise")
            throw new ForbiddenException("Daybook Activity Audit requires an Enterprise subscription.");

        if (toDate < fromDate)
            throw new ValidationException("toDate must be greater than or equal to fromDate.");

        var entries = await _context.DaybookEntries
            .AsNoTracking()
            .Where(de => de.OrganisationId == organisationId &&
                         de.EntryDate >= fromDate &&
                         de.EntryDate <= toDate)
            .OrderBy(de => de.EntryDate)
            .ThenBy(de => de.CreatedAt)
            .ToListAsync();

        var response = new DaybookAuditResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalEntries = entries.Count,
            PostedEntries = entries.Count(e => e.IsPosted),
            DraftEntries = entries.Count(e => !e.IsPosted)
        };

        foreach (var entry in entries)
        {
            string customerName = string.Empty;
            if (entry.CustomerId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(entry.CustomerId.Value);
                customerName = customer?.Name ?? string.Empty;
            }

            string supplierName = string.Empty;
            if (entry.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers.FindAsync(entry.SupplierId.Value);
                supplierName = supplier?.Name ?? string.Empty;
            }

            var journalLines = await _context.JournalEntries
                .Where(je => je.DaybookEntryId == entry.Id)
                .ToListAsync();

            response.Entries.Add(new DaybookAuditLine
            {
                EntryId = entry.Id,
                Type = entry.Type,
                ReferenceNumber = entry.ReferenceNumber ?? string.Empty,
                ExternalReference = entry.ExternalReference ?? string.Empty,
                EntryDate = entry.EntryDate,
                CreatedAt = entry.CreatedAt,
                Description = entry.Description ?? string.Empty,
                IsPosted = entry.IsPosted,
                CustomerName = customerName,
                SupplierName = supplierName,
                TotalDebits = journalLines.Sum(je => je.DebitAmount),
                TotalCredits = journalLines.Sum(je => je.CreditAmount),
                LineCount = journalLines.Count
            });
        }

        return response;
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

    public async Task<CustomerLedgerResponse> GetCustomerLedgerAsync(Guid organisationId, Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.OrganisationId == organisationId);
        if (customer == null)
            throw new ResourceNotFoundException("Customer", customerId.ToString());

        var entries = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Include(e => e.LinkedDaybookEntry)
            .Where(e => e.CustomerId == customerId)
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var lines = new List<LedgerLineResponse>();
        decimal runningBalance = 0;

        foreach (var entry in entries)
        {
            var (debit, credit) = GetArAmounts(entry.JournalEntries, entry.Type, customer.ControlAccountId);
            runningBalance += debit - credit;

            lines.Add(new LedgerLineResponse
            {
                EntryId = entry.Id,
                Type = entry.Type,
                Date = entry.EntryDate,
                Reference = entry.ReferenceNumber,
                Description = entry.Description,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                IsPosted = entry.IsPosted,
                LinkedEntryId = entry.LinkedDaybookEntryId,
                LinkedReference = entry.LinkedDaybookEntry?.ReferenceNumber
            });
        }

        // Also include receipts that are linked to this customer's invoices but may not have CustomerId set
        var linkedReceipts = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Where(e => e.OrganisationId == organisationId
                     && e.CustomerId == null
                     && e.LinkedDaybookEntryId != null
                     && entries.Select(inv => inv.Id).Contains(e.LinkedDaybookEntryId!.Value))
            .ToListAsync();

        foreach (var receipt in linkedReceipts)
        {
            // Already included if CustomerId was set; skip if already in list
            if (entries.Any(e => e.Id == receipt.Id)) continue;

            var (debit, credit) = GetArAmounts(receipt.JournalEntries, receipt.Type, customer.ControlAccountId);
            runningBalance += debit - credit;

            lines.Add(new LedgerLineResponse
            {
                EntryId = receipt.Id,
                Type = receipt.Type,
                Date = receipt.EntryDate,
                Reference = receipt.ReferenceNumber,
                Description = receipt.Description,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                IsPosted = receipt.IsPosted,
                LinkedEntryId = receipt.LinkedDaybookEntryId,
                LinkedReference = entries.FirstOrDefault(e => e.Id == receipt.LinkedDaybookEntryId)?.ReferenceNumber
            });
        }

        lines = lines.OrderBy(l => l.Date).ThenBy(l => l.Type).ToList();
        // Recalculate running balance after sorting
        decimal balance = 0;
        foreach (var line in lines)
        {
            balance += line.Debit - line.Credit;
            line.RunningBalance = balance;
        }

        return new CustomerLedgerResponse
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            Lines = lines,
            ClosingBalance = balance
        };
    }

    public async Task<SupplierLedgerResponse> GetSupplierLedgerAsync(Guid organisationId, Guid supplierId)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.OrganisationId == organisationId);
        if (supplier == null)
            throw new ResourceNotFoundException("Supplier", supplierId.ToString());

        var entries = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Include(e => e.LinkedDaybookEntry)
            .Where(e => e.SupplierId == supplierId)
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var lines = new List<LedgerLineResponse>();
        decimal runningBalance = 0;

        foreach (var entry in entries)
        {
            var (debit, credit) = GetApAmounts(entry.JournalEntries, entry.Type, supplier.ControlAccountId);
            // For AP: credit increases balance (we owe more), debit reduces it
            runningBalance += credit - debit;

            lines.Add(new LedgerLineResponse
            {
                EntryId = entry.Id,
                Type = entry.Type,
                Date = entry.EntryDate,
                Reference = entry.ReferenceNumber,
                Description = entry.Description,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                IsPosted = entry.IsPosted,
                LinkedEntryId = entry.LinkedDaybookEntryId,
                LinkedReference = entry.LinkedDaybookEntry?.ReferenceNumber
            });
        }

        // Also include payments linked to this supplier's invoices but may not have SupplierId set
        var linkedPayments = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Where(e => e.OrganisationId == organisationId
                     && e.SupplierId == null
                     && e.LinkedDaybookEntryId != null
                     && entries.Select(inv => inv.Id).Contains(e.LinkedDaybookEntryId!.Value))
            .ToListAsync();

        foreach (var payment in linkedPayments)
        {
            if (entries.Any(e => e.Id == payment.Id)) continue;

            var (debit, credit) = GetApAmounts(payment.JournalEntries, payment.Type, supplier.ControlAccountId);
            runningBalance += credit - debit;

            lines.Add(new LedgerLineResponse
            {
                EntryId = payment.Id,
                Type = payment.Type,
                Date = payment.EntryDate,
                Reference = payment.ReferenceNumber,
                Description = payment.Description,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                IsPosted = payment.IsPosted,
                LinkedEntryId = payment.LinkedDaybookEntryId,
                LinkedReference = entries.FirstOrDefault(e => e.Id == payment.LinkedDaybookEntryId)?.ReferenceNumber
            });
        }

        lines = lines.OrderBy(l => l.Date).ThenBy(l => l.Type).ToList();
        decimal balance = 0;
        foreach (var line in lines)
        {
            balance += line.Credit - line.Debit;
            line.RunningBalance = balance;
        }

        return new SupplierLedgerResponse
        {
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            Lines = lines,
            ClosingBalance = balance
        };
    }

    public async Task<IEnumerable<OutstandingInvoiceResponse>> GetOutstandingInvoicesAsync(Guid organisationId)
    {
        var invoices = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Include(e => e.Customer)
            .Where(e => e.OrganisationId == organisationId && e.Type == "Sales")
            .OrderBy(e => e.EntryDate)
            .ToListAsync();

        var invoiceIds = invoices.Select(e => e.Id).ToList();

        // Load all receipts that are linked to these invoices
        var linkedReceipts = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Where(e => e.OrganisationId == organisationId
                     && e.Type == "Receipt"
                     && e.LinkedDaybookEntryId != null
                     && invoiceIds.Contains(e.LinkedDaybookEntryId!.Value))
            .ToListAsync();

        var result = new List<OutstandingInvoiceResponse>();

        foreach (var invoice in invoices)
        {
            var controlAccountId = invoice.Customer?.ControlAccountId;
            var (debit, _) = GetArAmounts(invoice.JournalEntries, invoice.Type, controlAccountId);
            if (debit <= 0) continue;

            var receiptsForInvoice = linkedReceipts
                .Where(r => r.LinkedDaybookEntryId == invoice.Id)
                .ToList();

            decimal totalSettled = receiptsForInvoice
                .Sum(r => GetArAmounts(r.JournalEntries, r.Type, controlAccountId).credit);

            decimal outstanding = debit - totalSettled;
            if (outstanding <= 0) continue;

            result.Add(new OutstandingInvoiceResponse
            {
                EntryId = invoice.Id,
                Date = invoice.EntryDate,
                Reference = invoice.ReferenceNumber,
                Description = invoice.Description,
                CustomerId = invoice.CustomerId,
                CustomerName = invoice.Customer?.Name ?? "(No customer)",
                InvoiceTotal = debit,
                TotalSettled = totalSettled,
                Outstanding = outstanding,
                IsPosted = invoice.IsPosted
            });
        }

        return result;
    }

    public async Task<IEnumerable<OutstandingInvoiceResponse>> GetOutstandingBillsAsync(Guid organisationId)
    {
        var bills = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Include(e => e.Supplier)
            .Where(e => e.OrganisationId == organisationId && e.Type == "Purchase")
            .OrderBy(e => e.EntryDate)
            .ToListAsync();

        var billIds = bills.Select(e => e.Id).ToList();

        var linkedPayments = await _context.DaybookEntries
            .Include(e => e.JournalEntries)
            .Where(e => e.OrganisationId == organisationId
                     && e.Type == "Payment"
                     && e.LinkedDaybookEntryId != null
                     && billIds.Contains(e.LinkedDaybookEntryId!.Value))
            .ToListAsync();

        var result = new List<OutstandingInvoiceResponse>();

        foreach (var bill in bills)
        {
            var controlAccountId = bill.Supplier?.ControlAccountId;
            var (_, credit) = GetApAmounts(bill.JournalEntries, bill.Type, controlAccountId);
            if (credit <= 0) continue;

            var paymentsForBill = linkedPayments
                .Where(p => p.LinkedDaybookEntryId == bill.Id)
                .ToList();

            decimal totalSettled = paymentsForBill
                .Sum(p => GetApAmounts(p.JournalEntries, p.Type, controlAccountId).debit);

            decimal outstanding = credit - totalSettled;
            if (outstanding <= 0) continue;

            result.Add(new OutstandingInvoiceResponse
            {
                EntryId = bill.Id,
                Date = bill.EntryDate,
                Reference = bill.ReferenceNumber,
                Description = bill.Description,
                SupplierId = bill.SupplierId,
                SupplierName = bill.Supplier?.Name ?? "(No supplier)",
                InvoiceTotal = credit,
                TotalSettled = totalSettled,
                Outstanding = outstanding,
                IsPosted = bill.IsPosted
            });
        }

        return result;
    }

    /// <summary>
    /// Returns (debit, credit) for the AR side of a customer daybook entry.
    /// When a control account is set, filters to journal lines on that account.
    /// When no control account is set, falls back to type-based inference:
    ///   Sales/Receipt have one structurally-predictable debit/credit line for the receivable.
    /// </summary>
    private static (decimal debit, decimal credit) GetArAmounts(
        IEnumerable<JournalEntry> journalEntries,
        string entryType,
        Guid? controlAccountId)
    {
        var lines = journalEntries.ToList();

        if (controlAccountId.HasValue)
        {
            var ctrl = lines.Where(je => je.GLAccountId == controlAccountId.Value).ToList();
            return (ctrl.Sum(je => je.DebitAmount), ctrl.Sum(je => je.CreditAmount));
        }

        // Fallback: infer from entry structure
        // Sales:       DR Receivable (1 line) / CR Revenue+VAT (n lines) → debit = all debits
        // SalesReturn: CR Receivable (1 line) / DR Returns+VAT (n lines) → credit = all credits
        // Receipt:     DR Bank (1 line) / CR Receivable (1 line)         → credit = all credits
        return entryType switch
        {
            "Sales"       => (lines.Sum(je => je.DebitAmount),  0m),
            "SalesReturn" => (0m, lines.Sum(je => je.CreditAmount)),
            "Receipt"     => (0m, lines.Sum(je => je.CreditAmount)),
            _             => (0m, 0m)
        };
    }

    /// <summary>
    /// Returns (debit, credit) for the AP side of a supplier daybook entry.
    /// </summary>
    private static (decimal debit, decimal credit) GetApAmounts(
        IEnumerable<JournalEntry> journalEntries,
        string entryType,
        Guid? controlAccountId)
    {
        var lines = journalEntries.ToList();

        if (controlAccountId.HasValue)
        {
            var ctrl = lines.Where(je => je.GLAccountId == controlAccountId.Value).ToList();
            return (ctrl.Sum(je => je.DebitAmount), ctrl.Sum(je => je.CreditAmount));
        }

        // Fallback: infer from entry structure
        // Purchase:       DR Expense+VAT (n lines) / CR Payable (1 line) → credit = all credits
        // PurchaseReturn: DR Payable (1 line) / CR Expense+VAT (n lines) → debit = all debits
        // Payment:        DR Payable (1 line) / CR Bank (1 line)         → debit = all debits
        return entryType switch
        {
            "Purchase"       => (0m, lines.Sum(je => je.CreditAmount)),
            "PurchaseReturn" => (lines.Sum(je => je.DebitAmount),  0m),
            "Payment"        => (lines.Sum(je => je.DebitAmount),  0m),
            _                => (0m, 0m)
        };
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
