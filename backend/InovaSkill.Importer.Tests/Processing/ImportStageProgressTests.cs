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
}
