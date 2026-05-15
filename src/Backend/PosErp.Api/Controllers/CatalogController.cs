using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PosErp.Application.Features.Catalog.Commands.ImportProducts;
using PosErp.Application.Features.Catalog.Queries.SearchProducts;
using Microsoft.AspNetCore.Authorization;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize] // Commented out for easier testing without token
public class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
    {
        var result = await _mediator.Send(new SearchProductsQuery(q ?? string.Empty, limit));
        return Ok(result);
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        var result = await _mediator.Send(new ImportProductsCommand(file));
        return Ok(result);
    }
}
