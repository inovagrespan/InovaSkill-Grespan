using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class FiscalMovementsProcessorPostgresTests
{
    [Fact]
    public void DataSourceLockKey_IsSharedByVersionsAndDifferentBetweenSources()
    {
        var fiscalSourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var anotherSourceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal(
            FiscalMovementsProcessor.ResolveDataSourceLockKey(fiscalSourceId),
            FiscalMovementsProcessor.ResolveDataSourceLockKey(fiscalSourceId));
        Assert.NotEqual(
            FiscalMovementsProcessor.ResolveDataSourceLockKey(fiscalSourceId),
            FiscalMovementsProcessor.ResolveDataSourceLockKey(anotherSourceId));
    }

    [Fact]
    public async Task RealSpreadsheet_ProcessesBeyondFirstBatch_WhenPostgresIsConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable("FISCAL_IMPORT_TEST_CONNECTION");
        var filePath = Environment.GetEnvironmentVariable("FISCAL_IMPORT_TEST_FILE");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        var services = new ServiceCollection();
        services.AddDbContext<ImportDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<FiscalMovementsSpreadsheetParser>();
        services.AddScoped<IImportFileStorage>(_ => new FileStorage(filePath));
        services.AddScoped<FiscalMovementsProcessor>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
        await db.Database.MigrateAsync();
        var source = await db.DataSources.SingleAsync(item => item.Code == FiscalImportCodes.DataSource);
        var import = new RouteImport {
            Id = Guid.NewGuid(), DataSourceId = source.Id,
            Version = await db.RouteImports.Where(item => item.DataSourceId == source.Id).MaxAsync(item => item.Version) + 1,
            FileName = Path.GetFileName(filePath), FilePath = filePath,
            Status = RouteImportStatus.Processing, CreatedAt = DateTime.UtcNow, StartedAt = DateTime.UtcNow
        };
        db.RouteImports.Add(import);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<FiscalMovementsProcessor>()
            .ProcessAsync(import.Id, CancellationToken.None);

        db.ChangeTracker.Clear();
        var completed = await db.RouteImports.SingleAsync(item => item.Id == import.Id);
        Assert.Equal(RouteImportStatus.Completed, completed.Status);
        Assert.Equal(269_183, completed.ImportedRows);
        Assert.Equal(completed.TotalRows, completed.ImportedRows);
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*)::integer AS \"Value\" FROM fiscal_import_staging WHERE \"ImportId\" = {0}", import.Id).SingleAsync());
    }

    private sealed class FileStorage(string filePath) : IImportFileStorage
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(File.OpenRead(filePath));
    }
}
