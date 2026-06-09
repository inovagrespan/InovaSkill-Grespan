using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ImportDbContext dbContext) : ControllerBase
{
    private const int MinPageSize = 10;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var query = dbContext.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpper();
            query = query.Where(x => x.Sku.ToUpper().Contains(normalizedSearch) || x.Name.ToUpper().Contains(normalizedSearch));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Sku)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductDto(
                x.Id,
                x.Sku,
                x.Name,
                x.Price,
                x.CreatedAt,
                x.SourceFileJobId))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<ProductDto>(page, pageSize, total, items));
    }
}
