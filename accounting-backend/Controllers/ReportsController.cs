using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;
using AccountingApp.Filters;

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
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetTrialBalance(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetTrialBalanceAsync(organisationId, date);
        return Ok(result);
    }

    [HttpGet("taccounts")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetTAccounts(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetTAccountsAsync(organisationId, date);
        return Ok(result);
    }

    [HttpGet("taccounts/{accountId}")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetTAccount(Guid organisationId, Guid accountId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetTAccountAsync(accountId, date);
        return Ok(result);
    }

    [HttpGet("general-ledger")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetGeneralLedger(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetGeneralLedgerAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("profit-and-loss")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetProfitAndLoss(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetProfitAndLossAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("balance-sheet")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetBalanceSheet(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetBalanceSheetAsync(organisationId, date);
        return Ok(result);
    }

    // ── Pro Tier Reports ──────────────────────────────────────────────────────

    [HttpGet("aged-debtors")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetAgedDebtors(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetAgedDebtorsAsync(organisationId, date);
        return Ok(result);
    }

    [HttpGet("aged-creditors")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetAgedCreditors(Guid organisationId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        var result = await _service.GetAgedCreditorsAsync(organisationId, date);
        return Ok(result);
    }

    [HttpGet("vat-return")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetVatReturn(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetVatReturnAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("income-by-customer")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetIncomeByCustomer(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetIncomeByCustomerAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("spend-by-supplier")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetSpendBySupplier(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetSpendBySupplierAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    // ── Enterprise Tier Reports ───────────────────────────────────────────────

    [HttpGet("comparative-pl")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetComparativeProfitAndLoss(
        Guid organisationId,
        [FromQuery] DateTime period1From,
        [FromQuery] DateTime period1To,
        [FromQuery] DateTime period2From,
        [FromQuery] DateTime period2To)
    {
        var result = await _service.GetComparativeProfitAndLossAsync(organisationId, period1From, period1To, period2From, period2To);
        return Ok(result);
    }

    [HttpGet("cash-flow")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetCashFlowStatement(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetCashFlowStatementAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("account-activity")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetAccountActivitySummary(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetAccountActivitySummaryAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("revenue-breakdown")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetRevenueBreakdown(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetRevenueBreakdownAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("daybook-audit")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetDaybookAudit(Guid organisationId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _service.GetDaybookAuditAsync(organisationId, fromDate, toDate);
        return Ok(result);
    }
}
