using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/mapa-clientes")]
public sealed class MapaClientesController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetClientes(CancellationToken ct)
    {
        var clientes = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerName) && !string.IsNullOrWhiteSpace(x.City))
            .GroupBy(x => new { x.CustomerName, x.City })
            .Select(g => new
            {
                name = g.Key.CustomerName,
                city = g.Key.City,
                revenue = g.Sum(x => x.TotalAmount),
                orders = g.Count(),
                lastOrder = g.Max(x => x.TransactionDate)
            })
            .OrderByDescending(x => x.revenue)
            .Take(500)
            .ToListAsync(ct);

        return Ok(clientes);
    }

    [HttpGet("resumo")]
    public async Task<ActionResult> GetResumo(CancellationToken ct)
    {
        var cidades = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.City))
            .GroupBy(x => x.City)
            .Select(g => new
            {
                city = g.Key,
                totalCustomers = g.Select(x => x.CustomerName).Distinct().Count(),
                totalRevenue = g.Sum(x => x.TotalAmount),
                totalOrders = g.Count()
            })
            .OrderByDescending(x => x.totalRevenue)
            .ToListAsync(ct);

        return Ok(new
        {
            totalClientes = cidades.Sum(x => x.totalCustomers),
            totalCidades = cidades.Count,
            faturamentoTotal = cidades.Sum(x => x.totalRevenue),
            cidades
        });
    }
}
