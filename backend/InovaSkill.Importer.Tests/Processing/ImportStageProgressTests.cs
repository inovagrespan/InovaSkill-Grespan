using InovaSkill.Importer.Infrastructure.Processing;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class ImportStageProgressTests
{
    [Fact]
    public void CalculatePercent_ReturnsIntermediateProgressForSmallFiles()
    {
        Assert.Equal(33, ImportStageProgress.CalculatePercent(processedRows: 1, totalRows: 3));
        Assert.Equal(67, ImportStageProgress.CalculatePercent(processedRows: 2, totalRows: 3));
        Assert.Equal(100, ImportStageProgress.CalculatePercent(processedRows: 3, totalRows: 3));
    }

    [Fact]
    public void CalculatePercent_ClampsToStageBounds()
    {
        Assert.Equal(0, ImportStageProgress.CalculatePercent(processedRows: 0, totalRows: 0));
        Assert.Equal(100, ImportStageProgress.CalculatePercent(processedRows: 15, totalRows: 10));
    }

    [Fact]
    public void ShouldUpdate_ReturnsTrueWhenPercentChanges()
    {
        var currentPercent = ImportStageProgress.CalculatePercent(processedRows: 1, totalRows: 3);

        var shouldUpdate = ImportStageProgress.ShouldUpdate(
            processedRows: 1,
            totalRows: 3,
            currentPercent,
            lastPersistedPercent: 0);

        Assert.True(shouldUpdate);
    }

    [Fact]
    public void ShouldUpdate_ReturnsFalseWhenNoProgressChanged()
    {
        var shouldUpdate = ImportStageProgress.ShouldUpdate(
            processedRows: 1,
            totalRows: 10_000,
            currentPercent: 0,
            lastPersistedPercent: 0);

        Assert.False(shouldUpdate);
    }

    [Fact]
    public void CalculatePreProcessingCountingPercent_MovesWhileRowsAreBeingCounted()
    {
        Assert.Equal(0, ImportStageProgress.CalculatePreProcessingCountingPercent(countedRows: 0));
        Assert.Equal(1, ImportStageProgress.CalculatePreProcessingCountingPercent(countedRows: 1));
        Assert.Equal(2, ImportStageProgress.CalculatePreProcessingCountingPercent(countedRows: 10_000));
        Assert.Equal(ImportStageProgress.PreProcessingCountingMaxPercent, ImportStageProgress.CalculatePreProcessingCountingPercent(countedRows: 500_000));
    }

    [Fact]
    public void CalculatePreProcessingNormalizationPercent_ContinuesAfterCountingReservation()
    {
        Assert.Equal(ImportStageProgress.PreProcessingCountingMaxPercent, ImportStageProgress.CalculatePreProcessingNormalizationPercent(processedRows: 0, totalRows: 100));
        Assert.Equal(55, ImportStageProgress.CalculatePreProcessingNormalizationPercent(processedRows: 50, totalRows: 100));
        Assert.Equal(100, ImportStageProgress.CalculatePreProcessingNormalizationPercent(processedRows: 100, totalRows: 100));
    }

    [Fact]
    public void ShouldUpdateCounting_ReturnsTrueForHeartbeatEvenWhenPercentDidNotChange()
    {
        var shouldUpdate = ImportStageProgress.ShouldUpdateCounting(
            countedRows: ImportStageProgress.PreProcessingCountingHeartbeatRowInterval + 1,
            lastPersistedRows: 1,
            currentPercent: 1,
            lastPersistedPercent: 1);

        Assert.True(shouldUpdate);
    }
}
