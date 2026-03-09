using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;
using AccountingApp.Filters;

namespace AccountingApp.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations/{organisationId}/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerSupplierService _service;

    public CustomersController(ICustomerSupplierService service)
    {
        _service = service;
    }

    [HttpPost]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> CreateCustomer(Guid organisationId, [FromBody] CreateCustomerRequest request)
    {
        var result = await _service.CreateCustomerAsync(organisationId, request);
        return CreatedAtAction(nameof(GetCustomer), new { organisationId, customerId = result.Id }, result);
    }

    [HttpGet("{customerId}")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetCustomer(Guid organisationId, Guid customerId)
    {
        var customer = await _service.GetCustomerAsync(customerId);
        return Ok(customer);
    }

    [HttpGet]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetCustomers(Guid organisationId)
    {
        var customers = await _service.GetCustomersByOrganisationAsync(organisationId);
        return Ok(customers);
    }

    [HttpPut("{customerId}")]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> UpdateCustomer(Guid organisationId, Guid customerId, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _service.UpdateCustomerAsync(customerId, request);
        return Ok(result);
    }

    [HttpDelete("{customerId}")]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> DeleteCustomer(Guid organisationId, Guid customerId)
    {
        await _service.DeleteCustomerAsync(customerId);
        return NoContent();
    }

    [HttpGet("{customerId}/ledger")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetCustomerLedger(Guid organisationId, Guid customerId)
    {
        var result = await _service.GetCustomerLedgerAsync(organisationId, customerId);
        return Ok(result);
    }

    [HttpGet("outstanding")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetOutstandingInvoices(Guid organisationId)
    {
        var result = await _service.GetOutstandingInvoicesAsync(organisationId);
        return Ok(result);
    }
}

[Authorize]
[ApiController]
[Route("api/organisations/{organisationId}/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly ICustomerSupplierService _service;

    public SuppliersController(ICustomerSupplierService service)
    {
        _service = service;
    }

    [HttpPost]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> CreateSupplier(Guid organisationId, [FromBody] CreateSupplierRequest request)
    {
        var result = await _service.CreateSupplierAsync(organisationId, request);
        return CreatedAtAction(nameof(GetSupplier), new { organisationId, supplierId = result.Id }, result);
    }

    [HttpGet("{supplierId}")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetSupplier(Guid organisationId, Guid supplierId)
    {
        var supplier = await _service.GetSupplierAsync(supplierId);
        return Ok(supplier);
    }

    [HttpGet]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetSuppliers(Guid organisationId)
    {
        var suppliers = await _service.GetSuppliersByOrganisationAsync(organisationId);
        return Ok(suppliers);
    }

    [HttpPut("{supplierId}")]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> UpdateSupplier(Guid organisationId, Guid supplierId, [FromBody] UpdateSupplierRequest request)
    {
        var result = await _service.UpdateSupplierAsync(supplierId, request);
        return Ok(result);
    }

    [HttpDelete("{supplierId}")]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> DeleteSupplier(Guid organisationId, Guid supplierId)
    {
        await _service.DeleteSupplierAsync(supplierId);
        return NoContent();
    }

    [HttpGet("{supplierId}/ledger")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetSupplierLedger(Guid organisationId, Guid supplierId)
    {
        var result = await _service.GetSupplierLedgerAsync(organisationId, supplierId);
        return Ok(result);
    }

    [HttpGet("outstanding")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> GetOutstandingBills(Guid organisationId)
    {
        var result = await _service.GetOutstandingBillsAsync(organisationId);
        return Ok(result);
    }
}
