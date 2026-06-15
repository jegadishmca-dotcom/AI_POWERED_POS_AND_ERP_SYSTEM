using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PosErp.Application.Features.Catalog.Commands.ImportProducts;
using PosErp.Application.Features.Catalog.Commands.CreateProduct;
using PosErp.Application.Features.Catalog.Commands.UpdateProduct;
using PosErp.Application.Features.Catalog.Commands.DeleteProduct;
using PosErp.Application.Features.Catalog.Queries.SearchProducts;
using Microsoft.AspNetCore.Authorization;
using System;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // H1 FIX: Restored — was deliberately commented out for testing
public class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PosErp.Application.Interfaces.IApplicationDbContext _context;

    public CatalogController(IMediator mediator, PosErp.Application.Interfaces.IApplicationDbContext context)
    {
        _mediator = mediator;
        _context = context;
    }

    [HttpGet("search")]
    [AllowAnonymous] // POS terminal needs product search without pre-auth token on login screen
    public async Task<IActionResult> Search([FromQuery] string? q = "", [FromQuery] int limit = 20)
    {
        var result = await _mediator.Send(new SearchProductsQuery(q ?? string.Empty, limit));
        return Ok(result);
    }

    [HttpGet("tax-slabs")]
    [AllowAnonymous] // Tax slabs needed during cart calculation before auth in some flows
    public async Task<IActionResult> GetTaxSlabs()
    {
        var slabs = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Where(_context.TaxSlabs, t => !t.IsDeleted));
        return Ok(slabs);
    }

    [HttpGet("uoms")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUoms()
    {
        var uoms = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Where(_context.UnitOfMeasures, u => !u.IsDeleted));
        return Ok(uoms);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Where(_context.Categories, c => !c.IsDeleted));
        return Ok(categories);
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        var result = await _mediator.Send(new ImportProductsCommand(file));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("Mismatched Product ID.");
        }
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteProductCommand(id));
        return Ok(result);
    }
}
