using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class FiscalDocumentsControllerTests
{
    [Fact]
    public async Task Get_CalculatesObservableTotalsFromItemsWithoutUsingSourceTotal()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"fiscal-details-{Guid.NewGuid()}").Options);
        var now = DateTime.UtcNow;
        var source = new DataSource {
            Id = Guid.NewGuid(), Code = "FISCAL", ProcessorKey = "fiscal", Name = "Fiscal",
            Type = "EXCEL", ImportMode = DataSourceImportMode.Upsert, NextImportVersion = 2,
            Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "fiscal.xlsx",
            FilePath = "fiscal.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        var document = new FiscalDocument {
            Id = Guid.NewGuid(), DataSourceId = source.Id, DocumentNumber = "424808", Series = "1",
            DocumentType = "NF", MovementType = "NF", IssueDate = new DateOnly(2026, 7, 1),
            CustomerCodeAtIssue = "001091", BranchCodeAtIssue = "01", CustomerNameAtIssue = "Mercado",
            CityNameAtIssue = "Piraju", StateCodeAtIssue = "SP", OperationCode = "01",
            OperationDescription = "VENDA", MovementCategory = FiscalMovementCategory.Sale,
            FirstSeenImportId = import.Id, LastSeenImportId = import.Id, CreatedAt = now, UpdatedAt = now
        };
        document.Items.Add(Item("1", 2, 10, 5, -500));
        document.Items.Add(Item("2", 3, null, 3, 999_999));
        db.AddRange(source, import, document);
        await db.SaveChangesAsync();

        var result = await new FiscalDocumentsController(db).Get(document.Id, default);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"itemCount\":2", json);
        Assert.Contains("\"grossWeightKg\":8", json);
        Assert.Contains("\"totalQuantity\":5", json);
        Assert.Contains("\"calculatedTotalAmount\":20", json);
        Assert.Contains("\"calculatedAmount\":20", json);
        Assert.DoesNotContain("999999", json);
        Assert.DoesNotContain("-500", json);
    }

    private static FiscalDocumentItem Item(
        string number, decimal quantity, decimal? unitValue, decimal weight, decimal sourceTotal) => new() {
        Id = Guid.NewGuid(), ItemNumber = number, ProductCode = $"P{number}",
        ProductDescription = $"Produto {number}", ProductGroupCode = "G1",
        ProductGroupDescription = "Grupo", Quantity = quantity, UnitValue = unitValue,
        GrossWeightKg = weight, SourceTotalValue = sourceTotal,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
