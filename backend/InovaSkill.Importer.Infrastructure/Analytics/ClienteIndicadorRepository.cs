using InovaSkill.Importer.Application.Analytics.Indicadores;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Analytics;

public sealed class ClienteIndicadorRepository(ImportDbContext dbContext) : IClienteIndicadorRepository
{
    public async Task SalvarAsync(ClienteIndicador indicador, CancellationToken ct)
    {
        dbContext.Set<ClienteIndicador>().Add(indicador);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task SalvarBatchAsync(List<ClienteIndicador> indicadores, CancellationToken ct)
    {
        dbContext.Set<ClienteIndicador>().AddRange(indicadores);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task LimparAsync(CancellationToken ct)
    {
        dbContext.Set<ClienteIndicador>().RemoveRange(dbContext.Set<ClienteIndicador>());
        await dbContext.SaveChangesAsync(ct);
    }
}
