using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;
using AccountingApp.Filters;

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

    // Manual journal (explicit GL lines)
    [HttpPost]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> CreateEntry(Guid organisationId, [FromBody] CreateDaybookRequest request)
    {
        var result = await _service.CreateDaybookEntryAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    // Sales invoice: DR Receivable / CR Revenue / CR VAT
    [HttpPost("sales")]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> CreateSalesEntry(Guid organisationId, [FromBody] CreateSalesDaybookRequest request)
    {
        var result = await _service.CreateSalesDaybookAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    // Purchase invoice: DR Expense / DR VAT / CR Payable
    [HttpPost("purchases")]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> CreatePurchaseEntry(Guid organisationId, [FromBody] CreatePurchaseDaybookRequest request)
    {
        var result = await _service.CreatePurchaseDaybookAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    // Sales return / credit note: CR Receivable / DR Revenue / DR VAT
    [HttpPost("sales-returns")]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> CreateSalesReturnEntry(Guid organisationId, [FromBody] CreateSalesDaybookRequest request)
    {
        var result = await _service.CreateSalesReturnDaybookAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    // Purchase return: DR Payable / CR Expense / CR VAT
    [HttpPost("purchase-returns")]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> CreatePurchaseReturnEntry(Guid organisationId, [FromBody] CreatePurchaseDaybookRequest request)
    {
        var result = await _service.CreatePurchaseReturnDaybookAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    // Cash/bank receipt: DR Bank / CR Account
    [HttpPost("receipts")]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> CreateReceiptEntry(Guid organisationId, [FromBody] CreateReceiptDaybookRequest request)
    {
        var result = await _service.CreateReceiptDaybookAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    // Cash/bank payment: DR Account / CR Bank
    [HttpPost("payments")]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> CreatePaymentEntry(Guid organisationId, [FromBody] CreatePaymentDaybookRequest request)
    {
        var result = await _service.CreatePaymentDaybookAsync(organisationId, request);
        return CreatedAtAction(nameof(GetEntry), new { organisationId, entryId = result.Id }, result);
    }

    [HttpGet("{entryId}")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetEntry(Guid organisationId, Guid entryId)
    {
        var entry = await _service.GetDaybookEntryAsync(entryId);
        return Ok(entry);
    }

    [HttpGet]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetEntries(Guid organisationId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var entries = await _service.GetDaybookEntriesByOrganisationAsync(organisationId, fromDate, toDate);
        return Ok(entries);
    }

    [HttpPost("{entryId}/post")]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> PostEntry(Guid organisationId, Guid entryId)
    {
        await _service.PostDaybookEntryAsync(entryId);
        return Ok(new { message = "Entry posted successfully" });
    }

    [HttpDelete("{entryId}")]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> DeleteEntry(Guid organisationId, Guid entryId)
    {
        await _service.DeleteDaybookEntryAsync(entryId);
        return NoContent();
    }
}
