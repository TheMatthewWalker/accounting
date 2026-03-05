using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;
using AccountingApp.Data;
using AccountingApp.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AccountingApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrganisationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrganisationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganisation([FromBody] CreateOrganisationRequest request)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Forbid();
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

        // add creator as member
        var membership = new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            UserId = userId,
            Role = "Owner",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        _context.OrganisationMembers.Add(membership);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrganisation), new { id = org.Id }, org);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrganisation(Guid id)
    {
        var org = await _context.Organisations.FindAsync(id);
        if (org == null)
            return NotFound();

        return Ok(org);
    }

    [HttpGet]
    public async Task<IActionResult> ListOrganisations()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Forbid();
        }

        var orgs = await _context.OrganisationMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => m.Organisation)
            .ToListAsync();

        return Ok(orgs);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrganisation(Guid id, [FromBody] UpdateOrganisationRequest request)
    {
        var org = await _context.Organisations.FindAsync(id);
        if (org == null)
            return NotFound();

        org.Name = request.Name;
        org.Description = request.Description;
        org.RegistrationNumber = request.RegistrationNumber;
        org.TaxNumber = request.TaxNumber;

        await _context.SaveChangesAsync();
        return Ok(org);
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
}
