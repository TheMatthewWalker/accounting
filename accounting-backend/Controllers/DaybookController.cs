using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;

namespace AccountingApp.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations/{organisationId}/[controller]")]
public class DaybookController : ControllerBase
{
    private readonly IDaybookService _service;

    public DaybookController(IDaybookService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> CreateEntry(Guid organisationId, [FromBody] CreateDaybookRequest request)
    {
        var result = await _service.CreateDaybookEntryAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    [HttpGet("{entryId}")]
    public async Task<IActionResult> GetEntry(Guid organisationId, Guid entryId)
    {
        var entry = await _service.GetDaybookEntryAsync(entryId);
        return Ok(entry);
    }

    [HttpGet]
    public async Task<IActionResult> GetEntries(Guid organisationId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var entries = await _service.GetDaybookEntriesByOrganisationAsync(organisationId, fromDate, toDate);
        return Ok(entries);
    }

    [HttpPost("{entryId}/post")]
    public async Task<IActionResult> PostEntry(Guid organisationId, Guid entryId)
    {
        await _service.PostDaybookEntryAsync(entryId);
        return Ok(new { message = "Entry posted successfully" });
    }

    [HttpDelete("{entryId}")]
    public async Task<IActionResult> DeleteEntry(Guid organisationId, Guid entryId)
    {
        await _service.DeleteDaybookEntryAsync(entryId);
        return NoContent();
    }
}
