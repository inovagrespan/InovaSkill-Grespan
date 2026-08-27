using System.Text;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class HereCustomerCoordinatesImportTests
{
    [Fact]
    public void Parser_AcceptsExactAndInterpolatedAndIgnoresUnsafeStatuses()
    {
        using var stream = Csv(
            Row("251", "COMPRECENTER", "NÚMERO EXATO", "-22,41581", "-50,57769"),
            Row("252", "CLIENTE B", "NÚMERO INTERPOLADO", "-22,40000", "-50,50000"),
            Row("253", "CLIENTE C", "NÚMERO DIVERGENTE", "-22,30000", "-50,40000"));

        var result = new HereCustomerCoordinatesCsvParser().Parse(stream);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(1, result.IgnoredRows);
        Assert.Equal(-22.41581m, result.Rows[0].Latitude);
    }

    [Fact]
    public async Task Processor_UpsertsExactAndInterpolatedCoordinatesForCurrentCustomers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var customerSource = Source(CustomerImportCodes.DataSource, CustomerImportCodes.ProcessorKey, DataSourceImportMode.Snapshot, now);
        var customerImport = Import(customerSource.Id, "clientes.xlsx", now);
        var hereSource = Source(HereCustomerCoordinateImportCodes.DataSource, HereCustomerCoordinateImportCodes.ProcessorKey, DataSourceImportMode.Upsert, now);
        var hereImport = Import(hereSource.Id, "here.csv", now, RouteImportStatus.Processing);
        var city = new Municipality { Id = Guid.NewGuid(), StateCode = "SP", Name = "Paraguaçu Paulista",
            NormalizedName = "PARAGUACU PAULISTA", CreatedAt = now };
        var first = Customer(customerSource.Id, "000251");
        var second = Customer(customerSource.Id, "252");
        db.AddRange(customerSource, customerImport, hereSource, hereImport);
        await db.SaveChangesAsync();
        customerSource.CurrentImportId = customerImport.Id;
        db.AddRange(city, first, second,
            Snapshot(customerImport.Id, first.Id, city.Id), Snapshot(customerImport.Id, second.Id, city.Id));
        await db.SaveChangesAsync();
        var bytes = Encoding.UTF8.GetBytes(Header + Environment.NewLine +
            Row("251", "COMPRECENTER", "NÚMERO EXATO", "-22,41581", "-50,57769") + Environment.NewLine +
            Row("252", "CLIENTE B", "NÚMERO INTERPOLADO", "-22,40000", "-50,50000"));

        await new HereCustomerCoordinatesProcessor(db, new MemoryStorage(bytes), new HereCustomerCoordinatesCsvParser())
            .ProcessAsync(hereImport.Id, default);

        var coordinates = await db.CustomerAddressCoordinates.ToListAsync();
        Assert.Equal(2, coordinates.Count);
        Assert.Contains(coordinates, item => item.Precision == CustomerAddressCoordinatePrecisions.Exact && item.Latitude == -22.41581m);
        Assert.Contains(coordinates, item => item.Precision == CustomerAddressCoordinatePrecisions.Interpolated && item.Latitude == -22.4m);
        Assert.All(coordinates, item => Assert.Equal("HERE_IMPORT", item.Source));
        Assert.Equal(RouteImportStatus.Completed, (await db.RouteImports.FindAsync(hereImport.Id))!.Status);
    }

    private const string Header = "Linha;COD TOTVS;FANTASIA;CIDADE;ENDEREÇO ENTREGA;STATUS HERE;LATITUDE HERE;LONGITUDE HERE;ENDEREÇO HERE;ESTADO HERE;CEP HERE";
    private static string Row(string code, string name, string status, string latitude, string longitude) =>
        $"2;{code};{name};PARAGUACU PAULISTA;\"AV SIQUEIRA CAMPOS,698\";{status};{latitude};{longitude};\"Avenida Siqueira Campos, 698\";São Paulo;19700-019";
    private static MemoryStream Csv(params string[] rows) =>
        new(Encoding.UTF8.GetBytes(Header + Environment.NewLine + string.Join(Environment.NewLine, rows)));
    private static DataSource Source(string code, string processor, DataSourceImportMode mode, DateTime now) => new()
    { Id = Guid.NewGuid(), Code = code, ProcessorKey = processor, Name = code, Type = "CSV", ImportMode = mode,
        NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now };
    private static RouteImport Import(Guid sourceId, string file, DateTime now, RouteImportStatus status = RouteImportStatus.Completed) => new()
    { Id = Guid.NewGuid(), DataSourceId = sourceId, Version = 1, FileName = file, FilePath = file, Status = status, CreatedAt = now };
    private static Customer Customer(Guid sourceId, string code) => new()
    { Id = Guid.NewGuid(), DataSourceId = sourceId, ExternalCode = code, BranchCode = "01", CreatedAt = DateTime.UtcNow };
    private static CustomerSnapshot Snapshot(Guid importId, Guid customerId, Guid municipalityId) => new()
    { Id = Guid.NewGuid(), ImportId = importId, CustomerId = customerId, MunicipalityId = municipalityId,
        TradeName = "Cliente", LegalName = "Cliente", CustomerType = "Mercado", DocumentNumber = "",
        DocumentType = "UNKNOWN", SourceRowNumber = 1, CreatedAt = DateTime.UtcNow };
    private sealed class MemoryStorage(byte[] bytes) : IImportFileStorage
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
