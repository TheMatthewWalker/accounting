using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;

namespace AccountingApp.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations/{organisationId}/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;

    public ReportsController(IReportService service)
    {
        _service = service;
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetTrialBalanceAsync(organisationId, date);
        return Ok(result);
    }

    [HttpGet("taccounts")]
    public async Task<IActionResult> GetTAccounts(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetTAccountsAsync(organisationId, date);
        return Ok(result);
    }

    [HttpGet("taccounts/{accountId}")]
    public async Task<IActionResult> GetTAccount(Guid organisationId, Guid accountId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetTAccountAsync(accountId, date);
        return Ok(result);
    }

    [HttpGet("general-ledger")]
    public async Task<IActionResult> GetGeneralLedger(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetGeneralLedgerAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> GetProfitAndLoss(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetProfitAndLossAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> GetBalanceSheet(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetBalanceSheetAsync(organisationId, date);
        return Ok(result);
    }
}
