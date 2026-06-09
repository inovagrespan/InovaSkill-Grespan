using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Mappings;
using InovaSkill.Importer.Infrastructure.Parsing;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using InovaSkill.Importer.Infrastructure.Processing.TransformRules;
using InovaSkill.Importer.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ClosedXML.Excel;
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
            ImportFileTypeCode = ImportFileTypeCodes.Customers,
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

    [Fact]
    public async Task ProcessJobAsync_WhenWorkbookMatchesRoutePlanning_PersistsRoutePlanningImport()
    {
        await using var db = CreateDb();
        var directory = Path.Combine(Path.GetTempPath(), $"route-planning-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "rotas.xlsx");
        CreateRoutePlanningWorkbook(filePath);

        try
        {
            var job = new FileJob
            {
                FilePath = filePath,
                OriginalFileName = "Rotas por Cidades - Inova v2.xlsx",
                Status = FileJobStatus.WaitingProcessing,
                CurrentStep = "Aguardando processamento"
            };
            db.FileJobs.Add(job);
            await db.SaveChangesAsync();

            var processor = CreateProcessor(db);

            await processor.ProcessJobAsync(job.Id, CancellationToken.None);

            var afterValidation = await db.FileJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
            Assert.Equal(ImportFileTypeCodes.RoutePlanning, afterValidation.ImportFileTypeCode);
            Assert.Equal(FileJobStatus.ReadyToImport, afterValidation.Status);

            await processor.ProcessJobAsync(job.Id, CancellationToken.None);

            var completedJob = await db.FileJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
            Assert.Equal(FileJobStatus.Completed, completedJob.Status);
            Assert.Equal(2, completedJob.TotalRows);

            var routeImport = await db.RoutePlanningImports
                .Include(x => x.TruckCapacities)
                .Include(x => x.Routes)
                .ThenInclude(x => x.Stops)
                .SingleAsync();

            Assert.Equal(job.Id, routeImport.SourceFileJobId);
            Assert.Equal("Rotas por Cidades - Inova v2.xlsx", routeImport.SourceFileName);
            Assert.Single(routeImport.TruckCapacities);

            var route = Assert.Single(routeImport.Routes);
            Assert.Equal("RIO PRETO", route.RouteName);
            Assert.Equal("Truck", route.VehicleType);
            Assert.Equal(10300m, route.VehicleCapacityKg);
            Assert.Equal(61.35m, route.OccupancyPercent);
            Assert.Equal(2, route.Stops.Count);
            Assert.Contains(route.Stops, stop => stop.DestinationName == "BADY BASSITT");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessJobAsync_WhenExplicitRoutePlanningWorkbookIsUnknown_FailsJobWithClearReason()
    {
        await using var db = CreateDb();
        var directory = Path.Combine(Path.GetTempPath(), $"route-planning-unknown-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "nao-e-rota.xlsx");
        CreateNonRouteWorkbook(filePath);

        try
        {
            var job = new FileJob
            {
                FilePath = filePath,
                OriginalFileName = "nao-e-rota.xlsx",
                ImportFileTypeCode = ImportFileTypeCodes.RoutePlanning,
                Status = FileJobStatus.WaitingProcessing,
                CurrentStep = "Aguardando processamento"
            };
            db.FileJobs.Add(job);
            await db.SaveChangesAsync();

            var processor = CreateProcessor(db);

            await processor.ProcessJobAsync(job.Id, CancellationToken.None);

            var failedJob = await db.FileJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
            Assert.Equal(FileJobStatus.Failed, failedJob.Status);
            Assert.Contains("layout da planilha de rotas por cidade nao foi reconhecido", failedJob.CurrentStep, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessJobAsync_WhenSalesInvoiceImportCompletes_EnqueuesBothSummaryJobs()
    {
        await using var db = await CreateSqliteDbAsync();
        var normalizedFilePath = Path.Combine(Path.GetTempPath(), $"normalized-sales-summary-{Guid.NewGuid():N}.ndjson");
        await File.WriteAllLinesAsync(normalizedFilePath,
        [
            JsonSerializer.Serialize(new
            {
                rowNumber = 2,
                values = BuildSalesInvoiceRow(quantity: "2", unitPrice: "15.50", totalAmount: "31.00")
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ]);

        try
        {
            var publisher = new StubProcessingEventPublisher();
            var job = new FileJob
            {
                Id = 301,
                FilePath = "vendas.csv",
                OriginalFileName = "vendas.csv",
                NormalizedFilePath = normalizedFilePath,
                ImportFileTypeCode = ImportFileTypeCodes.SalesInvoice,
                Status = FileJobStatus.ReadyToImport,
                CurrentStep = "Aguardando importacao",
                TotalRows = 1
            };
            db.FileJobs.Add(job);
            await db.SaveChangesAsync();

            var processor = CreateProcessor(db, eventPublisher: publisher);
            await processor.ProcessJobAsync(job.Id, CancellationToken.None);

            var summaryEvents = publisher.Published
                .Where(x => x.EventType == ProcessingEventTypes.SummaryGenerationRequested)
                .ToList();

            var persistedJob = await db.FileJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
            Assert.Equal(FileJobStatus.Completed, persistedJob.Status);
            Assert.Equal(2, summaryEvents.Count);
            Assert.Contains(summaryEvents, x => HasJobType(x.Payload, PostImportJobType.SalesSummary));
            Assert.Contains(summaryEvents, x => HasJobType(x.Payload, PostImportJobType.CustomerSummary));
        }
        finally
        {
            File.Delete(normalizedFilePath);
        }
    }

    [Fact]
    public async Task ProcessJobAsync_WhenImportTypeIsNotSalesInvoice_DoesNotEnqueueSummaryJobs()
    {
        await using var db = await CreateSqliteDbAsync();
        var normalizedFilePath = Path.Combine(Path.GetTempPath(), $"normalized-customers-{Guid.NewGuid():N}.ndjson");
        await File.WriteAllLinesAsync(normalizedFilePath,
        [
            JsonSerializer.Serialize(new
            {
                rowNumber = 2,
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customercode"] = "C-001",
                    ["name"] = "Cliente Teste",
                    ["email"] = "cliente@example.com"
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ]);

        try
        {
            var publisher = new StubProcessingEventPublisher();
            var job = new FileJob
            {
                Id = 302,
                FilePath = "clientes.csv",
                OriginalFileName = "clientes.csv",
                NormalizedFilePath = normalizedFilePath,
                ImportFileTypeCode = ImportFileTypeCodes.Customers,
                Status = FileJobStatus.ReadyToImport,
                CurrentStep = "Aguardando importacao",
                TotalRows = 1
            };
            db.FileJobs.Add(job);
            await db.SaveChangesAsync();

            var processor = CreateProcessor(db, eventPublisher: publisher);
            await processor.ProcessJobAsync(job.Id, CancellationToken.None);

            Assert.DoesNotContain(
                publisher.Published,
                x => x.EventType == ProcessingEventTypes.SummaryGenerationRequested);
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

    private static FileImportPipelineProcessor CreateProcessor(
        ImportDbContext db,
        IFileParser? parser = null,
        IProcessingEventPublisher? eventPublisher = null)
    {
        return new FileImportPipelineProcessor(
            db,
            eventPublisher ?? new StubProcessingEventPublisher(),
            new StubFileJobProgressNotifier(),
            new StubFileParserFactory(parser),
            new RoutePlanningWorkbookParser(),
            new ImportPreProcessingPipeline(
                new StubTemplateResolver(),
                new ImportMappingEngine(new TransformRuleRegistry([new TrimRule()])),
                new StubFileTypeDetector()),
            new FileSchemaProvider(),
            new RowValidator(),
            new ConfigurationBuilder().Build(),
            NullLogger<FileImportPipelineProcessor>.Instance);
    }

    private static bool HasJobType(JsonElement? payload, PostImportJobType expectedJobType)
    {
        return payload is not null &&
            payload.Value.TryGetProperty("jobType", out var jobTypeElement) &&
            string.Equals(jobTypeElement.GetString(), expectedJobType.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"file-import-pipeline-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private static async Task<ImportDbContext> CreateSqliteDbAsync()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static void CreateRoutePlanningWorkbook(string filePath)
    {
        using var workbook = new XLWorkbook();

        var monday = workbook.AddWorksheet("SEGUNDA NOVA");
        monday.Cell(1, 2).Value = "ROTAS POR CIDADE E DIA DA SEMANA";
        monday.Cell(3, 2).Value = "SEGUNDA";
        monday.Cell(4, 2).Value = "RIO PRETO";
        monday.Cell(4, 3).Value = "CIDADES DA ROTA";
        monday.Cell(4, 4).Value = "Entregas";
        monday.Cell(4, 5).Value = "Media/Dia";
        monday.Cell(5, 3).Value = "SAO JOSE DO RIO PRETO";
        monday.Cell(5, 4).Value = 4;
        monday.Cell(5, 5).Value = 4_200m;
        monday.Cell(6, 3).Value = "BADY BASSITT";
        monday.Cell(6, 4).Value = 2;
        monday.Cell(6, 5).Value = 2_119.5m;
        monday.Cell(7, 2).Value = "Truck";
        monday.Cell(7, 4).Value = 6;
        monday.Cell(7, 5).Value = 6_319.5m;

        var capacity = workbook.AddWorksheet("Capacidade dos Caminhoes");
        capacity.Cell(3, 2).Value = "Caminhao";
        capacity.Cell(3, 3).Value = "Capacidade - KG";
        capacity.Cell(4, 2).Value = "Truck";
        capacity.Cell(4, 3).Value = 10_300m;

        workbook.SaveAs(filePath);
    }

    private static void CreateNonRouteWorkbook(string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Planilha1");
        sheet.Cell(1, 1).Value = "Codigo";
        sheet.Cell(1, 2).Value = "Descricao";
        sheet.Cell(2, 1).Value = "001";
        sheet.Cell(2, 2).Value = "Produto";
        workbook.SaveAs(filePath);
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

    private sealed class StubFileJobProgressNotifier : IFileJobProgressNotifier
    {
        public Task NotifyAsync(long jobId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubFileTypeDetector : IFileTypeDetector
    {
        public string? DetectCode(IReadOnlyDictionary<string, string> row)
        {
            return row.ContainsKey("documentnumber")
                ? ImportFileTypeCodes.SalesInvoice
                : null;
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
