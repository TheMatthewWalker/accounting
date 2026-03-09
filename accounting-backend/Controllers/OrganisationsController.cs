using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;
using AccountingApp.Data;
using AccountingApp.Models;
using AccountingApp.Exceptions;
using AccountingApp.Filters;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AccountingApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrganisationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IOrganisationService _orgService;

    public OrganisationsController(ApplicationDbContext context, IOrganisationService orgService)
    {
        _context = context;
        _orgService = orgService;
    }

    // ---- Organisation CRUD ----

    [HttpPost]
    public async Task<IActionResult> CreateOrganisation([FromBody] CreateOrganisationRequest request)
    {
        var userId = GetUserId();

        if (!string.IsNullOrWhiteSpace(request.RegistrationNumber))
        {
            var existingOrg = await _context.Organisations
                .Where(o => o.RegistrationNumber == request.RegistrationNumber && o.IsActive)
                .FirstOrDefaultAsync();

            if (existingOrg != null)
            {
                var ownerEmail = await _context.OrganisationMembers
                    .Where(m => m.OrganisationId == existingOrg.Id && m.Role == "Owner" && m.IsActive)
                    .Select(m => m.User!.Email)
                    .FirstOrDefaultAsync();

                var message = ownerEmail != null
                    ? $"An organisation with registration number '{request.RegistrationNumber}' already exists. To request access, contact the owner at {ownerEmail}."
                    : $"An organisation with registration number '{request.RegistrationNumber}' already exists.";

                throw new DuplicateResourceException(message);
            }
        }

        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            RegistrationNumber = request.RegistrationNumber,
            TaxNumber = request.TaxNumber,
            SubscriptionTier = "Free",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Organisations.Add(org);

        _context.OrganisationMembers.Add(new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            UserId = userId,
            Role = "Owner",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        });

        // Save org first so GLAccounts can reference it
        await _context.SaveChangesAsync();

        // Auto-create the default chart of accounts
        var defaultAccounts = GetDefaultChartOfAccounts(org.Id);
        _context.GLAccounts.AddRange(defaultAccounts);
        await _context.SaveChangesAsync();

        // Now set DefaultVatAccountId and save again
        org.DefaultVatAccountId = defaultAccounts.First(a => a.Code == "2100").Id;
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrganisation), new { id = org.Id }, org);
    }

    [HttpGet("{id:guid}")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetOrganisation(Guid id)
    {
        var org = await _context.Organisations.FindAsync(id);
        if (org == null || !org.IsActive)
            return NotFound();
        return Ok(org);
    }

    [HttpGet]
    public async Task<IActionResult> ListOrganisations()
    {
        var userId = GetUserId();

        var orgs = await _context.OrganisationMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => m.Organisation)
            .Where(o => o != null && o.IsActive)
            .ToListAsync();

        return Ok(orgs);
    }

    [HttpPut("{id:guid}")]
    [RequireOrganisationRole("Owner")]
    public async Task<IActionResult> UpdateOrganisation(Guid id, [FromBody] UpdateOrganisationRequest request)
    {
        var org = await _context.Organisations.FindAsync(id);
        if (org == null || !org.IsActive)
            return NotFound();

        org.Name = request.Name;
        org.Description = request.Description;
        org.RegistrationNumber = request.RegistrationNumber;
        org.TaxNumber = request.TaxNumber;
        org.DefaultVatAccountId = request.DefaultVatAccountId;
        org.VatReducedRate = request.VatReducedRate;
        org.VatFullRate = request.VatFullRate;

        await _context.SaveChangesAsync();
        return Ok(org);
    }

    [HttpDelete("{id:guid}")]
    [RequireOrganisationRole("Owner")]
    public async Task<IActionResult> DeleteOrganisation(Guid id)
    {
        await _orgService.DeleteOrganisationAsync(id);
        return NoContent();
    }

    // ---- Member management ----

    [HttpGet("{id:guid}/members")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await _orgService.GetMembersAsync(id);
        return Ok(members);
    }

    [HttpPut("{id:guid}/members/{memberId:guid}")]
    [RequireOrganisationRole("Owner")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid memberId, [FromBody] UpdateMemberRoleRequest request)
    {
        var result = await _orgService.UpdateMemberRoleAsync(id, memberId, request);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [RequireOrganisationRole("Owner")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        var userId = GetUserId();
        await _orgService.RemoveMemberAsync(id, memberId, userId);
        return NoContent();
    }

    // ---- Invitations ----

    [HttpGet("{id:guid}/invitations")]
    [RequireOrganisationRole("Owner")]
    public async Task<IActionResult> GetInvitations(Guid id)
    {
        var invitations = await _orgService.GetInvitationsAsync(id);
        return Ok(invitations);
    }

    [HttpPost("{id:guid}/invitations")]
    [RequireOrganisationRole("Owner")]
    public async Task<IActionResult> CreateInvitation(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var userId = GetUserId();
        var result = await _orgService.CreateInvitationAsync(id, userId, request);
        return CreatedAtAction(nameof(GetInvitations), new { id }, result);
    }

    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    [RequireOrganisationRole("Owner")]
    public async Task<IActionResult> CancelInvitation(Guid id, Guid invitationId)
    {
        await _orgService.CancelInvitationAsync(id, invitationId);
        return NoContent();
    }

    // Accept an invitation by token — no org role required (accepting user isn't a member yet)
    [HttpPost("invitations/accept")]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest request)
    {
        var userId = GetUserId();
        var result = await _orgService.AcceptInvitationAsync(request.Token, userId);
        return Ok(result);
    }

    // ---- Helpers ----

    private static List<GLAccount> GetDefaultChartOfAccounts(Guid organisationId)
    {
        var accounts = new[]
        {
            // Assets
            ("1000", "Bank",                     "Asset",   "Current Asset"),
            ("1010", "Cash",                     "Asset",   "Current Asset"),
            ("1100", "Trade Receivables",        "Asset",   "Current Asset"),
            ("1200", "Inventory",                "Asset",   "Current Asset"),
            ("1300", "Prepaid Expenses",         "Asset",   "Current Asset"),
            ("1400", "Property & Furniture",     "Asset",   "Fixed Asset"),
            ("1410", "Tools & Equipment",        "Asset",   "Fixed Asset"),
            // Liabilities
            ("2000", "Trade Payables",           "Liability", "Current Liability"),
            ("2100", "VAT Control Account",      "Liability", "Current Liability"),
            ("2200", "Accrued Expenses",         "Liability", "Current Liability"),
            // Equity
            ("3000", "Capital",                  "Equity",  null!),
            ("3100", "Drawings",                 "Equity",  null!),
            // Revenue
            ("4000", "Sales",                    "Revenue", null!),
            ("5200", "Discounts Received",       "Revenue", "Other Income"),
            // Expenses
            ("4100", "Sales Returns",            "Expense", "Cost of Sales"),
            ("4200", "Discounts Allowed",        "Expense", "Operating Expense"),
            ("5000", "Purchases",                "Expense", "Cost of Sales"),
            ("5100", "Purchase Returns",         "Expense", "Cost of Sales"),
            ("5300", "Cost of Goods Sold",       "Expense", "Cost of Sales"),
            ("6000", "Stationery & Sundries",    "Expense", "Operating Expense"),
            // Control
            ("9000", "Suspense Account",         "Asset",   "Current Asset"),
        };

        return accounts.Select(a => new GLAccount
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Code = a.Item1,
            Name = a.Item2,
            Type = a.Item3,
            SubType = a.Item4,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedException();
        return userId;
    }
}

public class CreateOrganisationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
}

public class UpdateOrganisationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public Guid? DefaultVatAccountId { get; set; }
    [Range(0, 100)]
    public decimal VatReducedRate { get; set; } = 5m;
    [Range(0, 100)]
    public decimal VatFullRate { get; set; } = 20m;
}

public class AcceptInvitationRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
