using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class ClienteAnalyticsPersistenceTests
{
    [Fact]
    public async Task Context_PersistsClienteIndicadorAndForecast()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();

        db.ClienteIndicadores.Add(new ClienteIndicador
        {
            ClienteId = "C001",
            Faturamento3M = 300,
            Faturamento6M = 600,
            Faturamento12M = 1200,
            Crescimento3M = 10,
            Crescimento6M = 15,
            Crescimento12M = 20,
            MediaMovel3M = 100,
            MediaMovel6M = 100,
            MediaMovel12M = 100,
            FrequenciaCompra = 2,
            TicketMedioGeral = 50,
            ScoreCrescimento = 80,
            ScoreFrequencia = 60,
            ScoreTicket = 40,
            ScoreRecencia = 100,
            ScorePotencial = 72,
            Tendencia = "Crescimento",
            Classificacao = "B",
            AtualizadoEm = DateTime.UtcNow
        });
        db.ClienteForecasts.Add(new ClienteForecast
        {
            ClienteId = "C001",
            Previsao30Dias = 110,
            Previsao60Dias = 120,
            Previsao90Dias = 130,
            TendenciaPrevista = "Crescimento",
            ErroMedioHistorico = 5,
            ConfiancaModelo = 90,
            UltimaObservacao = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var indicador = await db.ClienteIndicadores.SingleAsync(x => x.ClienteId == "C001");
        var forecast = await db.ClienteForecasts.SingleAsync(x => x.ClienteId == "C001");

        Assert.Equal(72, indicador.ScorePotencial);
        Assert.Equal("B", indicador.Classificacao);
        Assert.Equal(120, forecast.Previsao60Dias);
        Assert.Equal("Crescimento", forecast.TendenciaPrevista);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new ImportDbContext(options);
        db.Database.OpenConnection();
        return db;
    }
}
