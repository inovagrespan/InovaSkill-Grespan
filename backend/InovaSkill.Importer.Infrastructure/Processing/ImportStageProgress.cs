namespace InovaSkill.Importer.Infrastructure.Processing;

internal static class ImportStageProgress
{
    public const int StartPercent = 0;
    public const int CompletePercent = 100;

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
}
