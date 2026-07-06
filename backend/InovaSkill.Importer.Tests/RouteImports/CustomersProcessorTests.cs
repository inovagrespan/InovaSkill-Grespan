using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomersProcessorTests
{
    [Fact]
    public async Task ProcessAsync_PreservesCodes_ReusesMunicipality_AndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource,
            ProcessorKey = CustomerImportCodes.ProcessorKey, Name = "Clientes", Type = "EXCEL",
            ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2, Active = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "clientes.xlsx",
            FilePath = "customers", Status = RouteImportStatus.Processing, CreatedAt = DateTime.UtcNow
        };
        db.AddRange(source, import);
        await db.SaveChangesAsync();
        var processor = new CustomersProcessor(db, new MemoryStorage(CreateWorkbook()), new CustomersSpreadsheetParser());

        await processor.ProcessAsync(import.Id, default);
        await processor.ProcessAsync(import.Id, default);

        Assert.Equal(4, await db.Customers.CountAsync());
        Assert.Single(await db.Municipalities.ToListAsync());
        Assert.Equal(4, await db.CustomerSnapshots.CountAsync());
        var first = await db.CustomerSnapshots.Include(item => item.Customer)
            .SingleAsync(item => item.Customer!.ExternalCode == "000224");
        Assert.Equal("01", first.Customer!.BranchCode);
        Assert.Equal("07050702000200", first.DocumentNumber);
        Assert.Equal("CNPJ", first.DocumentType);
        Assert.Equal("CNPJ", (await db.CustomerSnapshots.Include(item => item.Customer)
            .SingleAsync(item => item.Customer!.ExternalCode == "000226")).DocumentType);
        Assert.Equal("UNKNOWN", (await db.CustomerSnapshots.Include(item => item.Customer)
            .SingleAsync(item => item.Customer!.ExternalCode == "000227")).DocumentType);
        Assert.Equal(2, await db.RouteImportErrors.CountAsync(item =>
            item.ImportId == import.Id && item.Status == ImportErrorStatus.Resolved));
    }

    private static byte[] CreateWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Clientes");
        var headers = new[] { "Codigo", "Loja", "CNPJ/CPF", "Nome", "N Fantasia", "Tipo", "Estado", "Municipio" };
        sheet.Cell(1, 1).Value = "Listagem do Browse";
        for (var index = 0; index < headers.Length; index++) sheet.Cell(2, index + 1).Value = headers[index];
        sheet.Cell(3, 1).Value = "000224"; sheet.Cell(3, 2).Value = "01";
        sheet.Cell(3, 3).Value = "07050702000200"; sheet.Cell(3, 4).Value = "PENIEL";
        sheet.Cell(3, 5).Value = "PENIEL 2"; sheet.Cell(3, 6).Value = "Solidario";
        sheet.Cell(3, 7).Value = "SP"; sheet.Cell(3, 8).Value = "BADY BASSITT";
        sheet.Cell(4, 1).Value = "000225"; sheet.Cell(4, 2).Value = "01";
        sheet.Cell(4, 3).Value = "07050702000200"; sheet.Cell(4, 4).Value = "OUTRO";
        sheet.Cell(4, 5).Value = "OUTRO"; sheet.Cell(4, 6).Value = "Solidario";
        sheet.Cell(4, 7).Value = "SP"; sheet.Cell(4, 8).Value = "  Bady   Bassitt ";
        sheet.Cell(5, 1).Value = "000226"; sheet.Cell(5, 2).Value = "01";
        sheet.Cell(5, 3).Value = "00000000000000"; sheet.Cell(5, 4).Value = "ZERO";
        sheet.Cell(5, 5).Value = "ZERO"; sheet.Cell(5, 6).Value = "Solidario";
        sheet.Cell(5, 7).Value = "SP"; sheet.Cell(5, 8).Value = "BADY BASSITT";
        sheet.Cell(6, 1).Value = "000227"; sheet.Cell(6, 2).Value = "01";
        sheet.Cell(6, 3).Value = "123"; sheet.Cell(6, 4).Value = "INVÁLIDO";
        sheet.Cell(6, 5).Value = "INVÁLIDO"; sheet.Cell(6, 6).Value = "Solidario";
        sheet.Cell(6, 7).Value = "SP"; sheet.Cell(6, 8).Value = "BADY BASSITT";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed class MemoryStorage(byte[] bytes) : IImportFileStorage
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
