using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class ImportLifecycleServiceTests
{
    [Fact]
    public async Task CreateAsync_AssignsIncreasingVersionWithinDataSource()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var source = await fixture.AddSourceAsync(DataSourceImportMode.Snapshot);
        var service = new ImportLifecycleService(fixture.Db);

        var first = await service.CreateAsync(source.Id, "first.xlsx", "first", default);
        var second = await service.CreateAsync(source.Id, "second.xlsx", "second", default);

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(3, (await fixture.Db.DataSources.SingleAsync()).NextImportVersion);
    }

    [Fact]
    public async Task TryActivateAsync_OlderImportFinishingLater_DoesNotReplaceCurrentSnapshot()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var source = await fixture.AddSourceAsync(DataSourceImportMode.Snapshot);
        var older = await fixture.AddImportAsync(source.Id, 10, RouteImportStatus.Completed);
        var newer = await fixture.AddImportAsync(source.Id, 11, RouteImportStatus.Completed);
        var service = new ImportLifecycleService(fixture.Db);

        Assert.True(await service.TryActivateAsync(newer.Id, default));
        Assert.False(await service.TryActivateAsync(older.Id, default));

        var state = await fixture.Db.DataSources.AsNoTracking().SingleAsync();
        Assert.Equal(newer.Id, state.CurrentImportId);
        Assert.Equal(newer.Id, state.LastSuccessfulImportId);
    }

    [Fact]
    public async Task TryActivateAsync_ImportNotCompleted_IsNotEligible()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var source = await fixture.AddSourceAsync(DataSourceImportMode.Snapshot);
        var processing = await fixture.AddImportAsync(source.Id, 1, RouteImportStatus.Processing);

        var activated = await new ImportLifecycleService(fixture.Db)
            .TryActivateAsync(processing.Id, default);

        Assert.False(activated);
        Assert.Null((await fixture.Db.DataSources.AsNoTracking().SingleAsync()).CurrentImportId);
    }

    [Fact]
    public async Task TryActivateAsync_AppendSource_TracksSuccessWithoutCurrentSnapshot()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var source = await fixture.AddSourceAsync(DataSourceImportMode.Append);
        var completed = await fixture.AddImportAsync(source.Id, 1, RouteImportStatus.Completed);

        var activated = await new ImportLifecycleService(fixture.Db)
            .TryActivateAsync(completed.Id, default);

        var state = await fixture.Db.DataSources.AsNoTracking().SingleAsync();
        Assert.False(activated);
        Assert.Null(state.CurrentImportId);
        Assert.Equal(completed.Id, state.LastSuccessfulImportId);
    }

    private sealed class DatabaseFixture(SqliteConnection connection, ImportDbContext db) : IAsyncDisposable
    {
        public ImportDbContext Db { get; } = db;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();
            return new DatabaseFixture(connection, db);
        }

        public async Task<DataSource> AddSourceAsync(DataSourceImportMode mode)
        {
            var source = new DataSource
            {
                Id = Guid.NewGuid(),
                Code = Guid.NewGuid().ToString("N"),
                ProcessorKey = "test",
                Name = "Test",
                Type = "XLSX",
                ImportMode = mode,
                NextImportVersion = 1,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            Db.DataSources.Add(source);
            await Db.SaveChangesAsync();
            return source;
        }

        public async Task<RouteImport> AddImportAsync(
            Guid sourceId,
            long version,
            RouteImportStatus status)
        {
            var routeImport = new RouteImport
            {
                Id = Guid.NewGuid(),
                DataSourceId = sourceId,
                Version = version,
                FileName = $"{version}.xlsx",
                FilePath = version.ToString(),
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            Db.RouteImports.Add(routeImport);
            await Db.SaveChangesAsync();
            return routeImport;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
