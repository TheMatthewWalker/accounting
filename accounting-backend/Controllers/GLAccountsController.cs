using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;

namespace AccountingApp.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations/{organisationId}/[controller]")]
public class GLAccountsController : ControllerBase
{
    private readonly IGLAccountService _service;

    public GLAccountsController(IGLAccountService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAccount(Guid organisationId, [FromBody] CreateGLAccountRequest request)
    {
        var result = await _service.CreateAccountAsync(organisationId, request);
        return CreatedAtAction(nameof(GetAccount), new { organisationId, accountId = result.Id }, result);
    }

    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetAccount(Guid organisationId, Guid accountId)
    {
        var account = await _service.GetAccountAsync(accountId);
        return Ok(account);
    }

    [HttpGet]
    public async Task<IActionResult> GetAccounts(Guid organisationId)
    {
        var accounts = await _service.GetAccountsByOrganisationAsync(organisationId);
        return Ok(accounts);
    }

    [HttpPut("{accountId}")]
    public async Task<IActionResult> UpdateAccount(Guid organisationId, Guid accountId, [FromBody] UpdateGLAccountRequest request)
    {
        var result = await _service.UpdateAccountAsync(accountId, request);
        return Ok(result);
    }

    [HttpDelete("{accountId}")]
    public async Task<IActionResult> DeleteAccount(Guid organisationId, Guid accountId)
    {
        await _service.DeleteAccountAsync(accountId);
        return NoContent();
    }
}
