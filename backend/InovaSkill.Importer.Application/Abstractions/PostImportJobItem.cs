namespace InovaSkill.Importer.Application.Abstractions;

public enum PostImportJobType
{
    SalesSummary = 1,
    CustomerSummary = 2
}

public sealed record PostImportJobItem(long FileJobId, PostImportJobType JobType);
