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
// [Authorize] // Commented out for easier testing without token
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
    public async Task<IActionResult> Search([FromQuery] string? q = "", [FromQuery] int limit = 20)
    {
        var result = await _mediator.Send(new SearchProductsQuery(q ?? string.Empty, limit));
        return Ok(result);
    }

    [HttpGet("tax-slabs")]
    public async Task<IActionResult> GetTaxSlabs()
    {
        var slabs = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Where(_context.TaxSlabs, t => !t.IsDeleted));
        return Ok(slabs);
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        var result = await _mediator.Send(new ImportProductsCommand(file));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
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
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteProductCommand(id));
        return Ok(result);
    }
}
