using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Detection;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.Detection;

public sealed class DetectionRunServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RunQueued_ExecutesSuccessfullyAndPersistsFindings()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var detector = new FixedResultDetector("TEST_DETECTOR", 2, 2);
        var definition = fixture.AddDetectorDefinition(detector.Code);
        var run = fixture.AddDetectionRun(definition.Id, DetectionRunStatus.Queued);
        await fixture.Db.SaveChangesAsync();

        var registry = new DetectorRegistry([detector]);
        var service = new DetectionRunService(fixture.Db, registry, NullLogger<DetectionRunService>.Instance);

        await service.ExecuteAsync(run.Id, default);

        var savedRun = await fixture.Db.DetectionRuns.AsNoTracking()
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(DetectionRunStatus.Succeeded, savedRun.Status);
        Assert.Equal(2, savedRun.AnalyzedItems);
        Assert.Equal(2, savedRun.FindingsCount);
        Assert.NotNull(savedRun.StartedAt);
        Assert.NotNull(savedRun.FinishedAt);
        Assert.Equal(1, savedRun.AttemptCount);

        var findings = await fixture.Db.Findings.AsNoTracking()
            .Where(x => x.DetectionRunId == run.Id)
            .ToListAsync();
        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.NotEmpty(f.Title));

        var evidences = await fixture.Db.FindingEvidences.AsNoTracking()
            .Where(e => findings.Select(f => f.Id).Contains(e.FindingId))
            .ToListAsync();
        Assert.Equal(2, evidences.Count);
    }

    [Fact]
    public async Task ExecuteAsync_RunAlreadySucceeded_DoesNothing()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var detector = new FixedResultDetector("TEST_DETECTOR", 10, 3);
        var definition = fixture.AddDetectorDefinition(detector.Code);
        var run = fixture.AddDetectionRun(definition.Id, DetectionRunStatus.Succeeded);
        run.AnalyzedItems = 10;
        run.FindingsCount = 3;
        await fixture.Db.SaveChangesAsync();

        var registry = new DetectorRegistry([detector]);
        var service = new DetectionRunService(fixture.Db, registry, NullLogger<DetectionRunService>.Instance);

        await service.ExecuteAsync(run.Id, default);

        var savedRun = await fixture.Db.DetectionRuns.AsNoTracking()
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(DetectionRunStatus.Succeeded, savedRun.Status);
        Assert.Equal(10, savedRun.AnalyzedItems);
        Assert.Equal(3, savedRun.FindingsCount);
    }

    [Fact]
    public async Task ExecuteAsync_DetectorNotFound_MarksRunAsFailed()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var definition = fixture.AddDetectorDefinition(DetectorCodes.CustomerPurchaseDrop);
        var run = fixture.AddDetectionRun(definition.Id, DetectionRunStatus.Queued);
        await fixture.Db.SaveChangesAsync();

        var registry = new DetectorRegistry([]);
        var service = new DetectionRunService(fixture.Db, registry, NullLogger<DetectionRunService>.Instance);

        await service.ExecuteAsync(run.Id, default);

        var savedRun = await fixture.Db.DetectionRuns.AsNoTracking()
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(DetectionRunStatus.Failed, savedRun.Status);
        Assert.NotNull(savedRun.StatusReason);
    }

    [Fact]
    public async Task ExecuteAsync_DetectorReturnsZeroFindings_SucceedsWithZeroCount()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var detector = new FixedResultDetector("EMPTY_DETECTOR", 0, 0);
        var definition = fixture.AddDetectorDefinition(detector.Code);
        var run = fixture.AddDetectionRun(definition.Id, DetectionRunStatus.Queued);
        await fixture.Db.SaveChangesAsync();

        var registry = new DetectorRegistry([detector]);
        var service = new DetectionRunService(fixture.Db, registry, NullLogger<DetectionRunService>.Instance);

        await service.ExecuteAsync(run.Id, default);

        var savedRun = await fixture.Db.DetectionRuns.AsNoTracking()
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(DetectionRunStatus.Succeeded, savedRun.Status);
        Assert.Equal(0, savedRun.AnalyzedItems);
        Assert.Equal(0, savedRun.FindingsCount);

        var findingsCount = await fixture.Db.Findings.CountAsync();
        Assert.Equal(0, findingsCount);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateFingerprintInResult_MarksRunAsFailed()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var detector = new DuplicateFingerprintDetector();
        var definition = fixture.AddDetectorDefinition(detector.Code);
        var run = fixture.AddDetectionRun(definition.Id, DetectionRunStatus.Queued);
        await fixture.Db.SaveChangesAsync();

        var registry = new DetectorRegistry([detector]);
        var service = new DetectionRunService(fixture.Db, registry, NullLogger<DetectionRunService>.Instance);

        await service.ExecuteAsync(run.Id, default);

        var savedRun = await fixture.Db.DetectionRuns.AsNoTracking()
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(DetectionRunStatus.Failed, savedRun.Status);
        Assert.Contains("Fingerprint duplicado", savedRun.StatusReason);
    }

    [Fact]
    public async Task ExecuteAsync_DetectorThrowsException_MarksRunAsFailed()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var detector = new ThrowingDetector();
        var definition = fixture.AddDetectorDefinition(detector.Code);
        var run = fixture.AddDetectionRun(definition.Id, DetectionRunStatus.Queued);
        await fixture.Db.SaveChangesAsync();

        var registry = new DetectorRegistry([detector]);
        var service = new DetectionRunService(fixture.Db, registry, NullLogger<DetectionRunService>.Instance);

        await service.ExecuteAsync(run.Id, default);

        var savedRun = await fixture.Db.DetectionRuns.AsNoTracking()
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(DetectionRunStatus.Failed, savedRun.Status);
        Assert.NotNull(savedRun.StatusReason);
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

        public DetectorDefinition AddDetectorDefinition(string code)
        {
            var now = DateTime.UtcNow;
            var definition = new DetectorDefinition
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = $"Test {code}",
                Description = null,
                Status = DetectorStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            Db.DetectorDefinitions.Add(definition);
            return definition;
        }

        public DetectionRun AddDetectionRun(Guid detectorDefinitionId, DetectionRunStatus status)
        {
            var now = DateTime.UtcNow;
            var run = new DetectionRun
            {
                Id = Guid.NewGuid(),
                DetectorDefinitionId = detectorDefinitionId,
                Status = status,
                Trigger = DetectionTrigger.Manual,
                RequestedAt = now,
                AttemptCount = 0
            };
            Db.DetectionRuns.Add(run);
            return run;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}

public sealed class FixedResultDetector(
    string code,
    int analyzedItems,
    int findingCount) : IDetector
{
    public string Code => code;

    public Task<DetectionResult> DetectAsync(
        DetectionContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<FindingCandidate>();
        for (var i = 0; i < findingCount; i++)
        {
            findings.Add(new FindingCandidate(
                Fingerprint: $"{code}:{i}",
                Title: $"Finding {i}",
                Description: $"Description {i}",
                SubjectType: "Test",
                SubjectId: i.ToString(),
                SubjectLabel: null,
                Evidences: new List<FindingEvidenceCandidate>
                {
                    new("Evidence", "1", null, null, null, null, null, context.ReferenceTime)
                }));
        }

        return Task.FromResult(new DetectionResult(analyzedItems, findings.AsReadOnly()));
    }
}

public sealed class DuplicateFingerprintDetector : IDetector
{
    public string Code => "DUPLICATE_FINGERPRINT";

    public Task<DetectionResult> DetectAsync(
        DetectionContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<FindingCandidate>
        {
            new("DUP:1", "Finding 1", "Desc 1", "Customer", "1", null, []),
            new("DUP:1", "Finding 2", "Desc 2", "Customer", "2", null, []),
        };
        return Task.FromResult(new DetectionResult(2, findings));
    }
}

public sealed class ThrowingDetector : IDetector
{
    public string Code => "THROWING_DETECTOR";

    public Task<DetectionResult> DetectAsync(
        DetectionContext context,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Simulated detector failure.");
    }
}
