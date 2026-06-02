using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Mappings;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using InovaSkill.Importer.Infrastructure.Processing.TransformRules;
using InovaSkill.Importer.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class FileImportPipelineProcessorTests
{
    [Fact]
    public async Task ProcessJobAsync_PersistsPreProcessingProgressWhileCountingRows()
    {
        await using var db = CreateDb();
        var directory = Path.Combine(Path.GetTempPath(), $"pre-processing-progress-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "clientes.csv");
        await File.WriteAllTextAsync(filePath, "placeholder");
        var snapshots = new List<FileJob>();

        var job = new FileJob
        {
            Id = 200,
            FilePath = filePath,
            OriginalFileName = "clientes.csv",
            ImportFileTypeCode = ImportFileTypeCodes.CustomerList,
            Status = FileJobStatus.WaitingProcessing,
            CurrentStep = "Aguardando processamento"
        };
        db.FileJobs.Add(job);
        await db.SaveChangesAsync();

        var parser = new SnapshotFileParser(
            rowCount: ImportStageProgress.PreProcessingCountingHeartbeatRowInterval + 2,
            captureBeforeFirstRow: async () =>
            {
                var snapshot = await db.FileJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
                snapshots.Add(snapshot);
            },
            captureAfterFirstCountingCheckpoint: async () =>
            {
                var snapshot = await db.FileJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
                snapshots.Add(snapshot);
            });
        var processor = CreateProcessor(db, parser);

        try
        {
            await processor.ProcessJobAsync(job.Id, CancellationToken.None);

            Assert.Contains(snapshots, snapshot =>
                snapshot.Status == FileJobStatus.PreProcessing &&
                snapshot.ProgressPercent > 0 &&
                snapshot.CurrentStep == "Lendo estrutura do arquivo" &&
                snapshot.ProcessedRows == 0);

            Assert.Contains(snapshots, snapshot =>
                snapshot.Status == FileJobStatus.PreProcessing &&
                snapshot.ProgressPercent > 0 &&
                snapshot.CurrentStep == "Contando linhas do arquivo" &&
                snapshot.ProcessedRows >= ImportStageProgress.PreProcessingCountingHeartbeatRowInterval);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateRowsBeforeImportAsync_ReturnsErrorsForOldNormalizedRowsThatWouldOverflowDatabaseNumericColumns()
    {
        await using var db = CreateDb();
        var normalizedFilePath = Path.Combine(Path.GetTempPath(), $"normalized-overflow-{Guid.NewGuid():N}.ndjson");
        await File.WriteAllLinesAsync(normalizedFilePath,
        [
            JsonSerializer.Serialize(new
            {
                rowNumber = 2,
                values = BuildSalesInvoiceRow(quantity: "999999999999999.999", unitPrice: "11.00", totalAmount: "1.00")
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ]);

        try
        {
            var job = new FileJob
            {
                Id = 123,
                FilePath = "vendas.csv",
                OriginalFileName = "vendas.csv",
                NormalizedFilePath = normalizedFilePath,
                ImportFileTypeCode = ImportFileTypeCodes.SalesInvoice,
                Status = FileJobStatus.Importing,
                CurrentStep = "Importando dados",
                TotalRows = 1
            };
            var processor = CreateProcessor(db);

            var errors = await processor.ValidateRowsBeforeImportAsync(job, CancellationToken.None);

            var error = Assert.Single(errors);
            Assert.Equal(ImportProcessingStages.Import, error.Stage);
            Assert.Equal("totalamount", error.Column);
            Assert.Contains("numeric(18,2)", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(normalizedFilePath);
        }
    }

    [Fact]
    public async Task ValidateRowsBeforeImportAsync_ReturnsErrorsForOldNormalizedRowsWithPtBrDecimalCommas()
    {
        await using var db = CreateDb();
        var normalizedFilePath = Path.Combine(Path.GetTempPath(), $"normalized-comma-{Guid.NewGuid():N}.ndjson");
        await File.WriteAllLinesAsync(normalizedFilePath,
        [
            JsonSerializer.Serialize(new
            {
                rowNumber = 4802,
                values = BuildSalesInvoiceRow(
                    quantity: "1",
                    unitPrice: "3,3200000000000003",
                    totalAmount: "10,620000000000001")
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ]);

        try
        {
            var job = new FileJob
            {
                Id = 124,
                FilePath = "vendas.csv",
                OriginalFileName = "vendas.csv",
                NormalizedFilePath = normalizedFilePath,
                ImportFileTypeCode = ImportFileTypeCodes.SalesInvoice,
                Status = FileJobStatus.Importing,
                CurrentStep = "Importando dados",
                TotalRows = 1
            };
            var processor = CreateProcessor(db);

            var errors = await processor.ValidateRowsBeforeImportAsync(job, CancellationToken.None);

            Assert.Contains(errors, error =>
                error.Stage == ImportProcessingStages.Import &&
                error.Column == "unitprice" &&
                error.Message.Contains("numeric(18,2)", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(normalizedFilePath);
        }
    }

    private static Dictionary<string, string> BuildSalesInvoiceRow(string quantity, string unitPrice, string totalAmount)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentnumber"] = "NF-001",
            ["transactiondate"] = "2026-05-20",
            ["customercode"] = "C-001",
            ["customername"] = "Cliente Teste",
            ["productcode"] = "P-001",
            ["productdescription"] = "Produto Teste",
            ["quantity"] = quantity,
            ["unitprice"] = unitPrice,
            ["totalamount"] = totalAmount,
            ["transactiontype"] = "N",
            ["city"] = "Sao Paulo",
            ["productgroup"] = "Grupo",
            ["grossweightkg"] = "1.000"
        };
    }

    private static FileImportPipelineProcessor CreateProcessor(ImportDbContext db, IFileParser? parser = null)
    {
        return new FileImportPipelineProcessor(
            db,
            new StubProcessingEventPublisher(),
            new StubFileParserFactory(parser),
            new ImportPreProcessingPipeline(
                new StubTemplateResolver(),
                new ImportMappingEngine(new TransformRuleRegistry([new TrimRule()])),
                new FileTypeDetector()),
            new FileSchemaProvider(),
            new RowValidator(),
            new ConfigurationBuilder().Build(),
            NullLogger<FileImportPipelineProcessor>.Instance);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"file-import-pipeline-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private sealed class StubProcessingEventPublisher : IProcessingEventPublisher
    {
        public List<ProcessingEventEnvelope> Published { get; } = [];

        public Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
        {
            Published.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class StubFileParserFactory(IFileParser? parser = null) : IFileParserFactory
    {
        public IFileParser Create(string filePath) => parser ?? throw new NotSupportedException();
    }

    private sealed class StubTemplateResolver : IPreProcessorTemplateResolver
    {
        public Task<ImportTemplate?> ResolveAsync(string fileName, IReadOnlyCollection<string> headers, CancellationToken cancellationToken)
        {
            return Task.FromResult<ImportTemplate?>(null);
        }
    }

    private sealed class SnapshotFileParser(
        int rowCount,
        Func<Task> captureBeforeFirstRow,
        Func<Task> captureAfterFirstCountingCheckpoint) : IFileParser
    {
        private int parseCount;

        public async IAsyncEnumerable<ImportedRow> ParseAsync(
            string filePath,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            parseCount++;
            var currentParseCount = parseCount;

            for (var i = 1; i <= rowCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentParseCount == 1 && i == 1)
                {
                    await captureBeforeFirstRow();
                }

                yield return new ImportedRow(i + 1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customercode"] = $"C-{i:D5}",
                    ["name"] = $"Cliente {i}",
                    ["email"] = $"cliente{i}@example.com"
                });

                if (currentParseCount == 1 && i == ImportStageProgress.PreProcessingCountingHeartbeatRowInterval + 1)
                {
                    await captureAfterFirstCountingCheckpoint();
                }
            }
        }
    }
}
