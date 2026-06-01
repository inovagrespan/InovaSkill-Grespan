namespace InovaSkill.Importer.Infrastructure.Processing;

internal static class ImportStageProgress
{
    public const int StartPercent = 0;
    public const int CompletePercent = 100;
    public const int PreProcessingCountingMaxPercent = 10;
    public const int PreProcessingCountingRowsPerPercent = 10_000;
    public const int PreProcessingCountingHeartbeatRowInterval = 5_000;

    public static int CalculatePercent(int processedRows, int totalRows)
    {
        if (totalRows <= 0)
        {
            return StartPercent;
        }

        var ratio = (double)processedRows / totalRows;
        var percent = (int)Math.Round(CompletePercent * ratio);
        return Math.Clamp(percent, StartPercent, CompletePercent);
    }

    public static bool ShouldUpdate(int processedRows, int totalRows, int currentPercent, int lastPersistedPercent)
    {
        if (processedRows <= 0)
        {
            return false;
        }

        if (processedRows >= totalRows && totalRows > 0)
        {
            return true;
        }

        return currentPercent != lastPersistedPercent;
    }

    public static int CalculatePreProcessingCountingPercent(int countedRows)
    {
        if (countedRows <= 0)
        {
            return StartPercent;
        }

        var percent = (countedRows / PreProcessingCountingRowsPerPercent) + 1;
        return Math.Clamp(percent, 1, PreProcessingCountingMaxPercent);
    }

    public static int CalculatePreProcessingNormalizationPercent(int processedRows, int totalRows)
    {
        if (totalRows <= 0)
        {
            return StartPercent;
        }

        var ratio = (double)processedRows / totalRows;
        var normalizationRange = CompletePercent - PreProcessingCountingMaxPercent;
        var percent = PreProcessingCountingMaxPercent + (int)Math.Round(normalizationRange * ratio);
        return Math.Clamp(percent, PreProcessingCountingMaxPercent, CompletePercent);
    }

    public static bool ShouldUpdateCounting(int countedRows, int lastPersistedRows, int currentPercent, int lastPersistedPercent)
    {
        if (countedRows <= 0)
        {
            return false;
        }

        return currentPercent != lastPersistedPercent ||
            countedRows - lastPersistedRows >= PreProcessingCountingHeartbeatRowInterval;
    }
}
