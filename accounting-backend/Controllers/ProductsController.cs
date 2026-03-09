using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingApp.Services;
using AccountingApp.Filters;

namespace AccountingApp.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations/{organisationId:guid}/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> List(Guid organisationId)
    {
        var result = await _service.GetByOrganisationAsync(organisationId);
        return Ok(result);
    }

    [HttpPost]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> Create(Guid organisationId, [FromBody] CreateProductServiceRequest request)
    {
        var result = await _service.CreateAsync(organisationId, request);
        return CreatedAtAction(nameof(Get), new { organisationId, productId = result.Id }, result);
    }

    [HttpGet("{productId:guid}")]
    [RequireOrganisationRole("Viewer")]
    public async Task<IActionResult> Get(Guid organisationId, Guid productId)
    {
        var result = await _service.GetAsync(productId);
        return Ok(result);
    }

    [HttpPut("{productId:guid}")]
    [RequireOrganisationRole("Bookkeeper")]
    public async Task<IActionResult> Update(Guid organisationId, Guid productId, [FromBody] UpdateProductServiceRequest request)
    {
        var result = await _service.UpdateAsync(productId, request);
        return Ok(result);
    }

    [HttpDelete("{productId:guid}")]
    [RequireOrganisationRole("Manager")]
    public async Task<IActionResult> Delete(Guid organisationId, Guid productId)
    {
        await _service.DeleteAsync(productId);
        return NoContent();
    }
}
