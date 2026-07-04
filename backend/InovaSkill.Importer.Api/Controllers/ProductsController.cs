using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ImportDbContext dbContext) : ControllerBase
{
    private const int MinimumPage = 1;
    private const int MinimumPageSize = 10;
    private const int MaximumPageSize = 100;
    private const int DefaultPageSize = 20;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged(
        [FromQuery] int page = MinimumPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] string? sku = null,
        [FromQuery] string? name = null,
        [FromQuery] decimal? priceMin = null,
        [FromQuery] decimal? priceMax = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(MinimumPage, page);
        pageSize = Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize);

        var query = dbContext.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Sku.ToLower().Contains(normalized) || x.Name.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            var normalized = sku.Trim().ToLowerInvariant();
            query = query.Where(x => x.Sku.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var normalized = name.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(normalized));
        }

        if (priceMin.HasValue)
        {
            query = query.Where(x => x.Price >= priceMin.Value);
        }

        if (priceMax.HasValue)
        {
            query = query.Where(x => x.Price <= priceMax.Value);
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
