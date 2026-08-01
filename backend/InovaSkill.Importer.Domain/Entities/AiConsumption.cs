namespace InovaSkill.Importer.Domain.Entities;

public sealed class AiResponseExecution
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public Guid? ChatSessionId { get; set; }
    public string Status { get; set; } = AiConsumptionStatuses.InProgress;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public AppUser User { get; set; } = null!;
    public ICollection<AiProviderCall> Calls { get; set; } = [];
}

public sealed class AiProviderCall
{
    public Guid Id { get; set; }
    public Guid ResponseExecutionId { get; set; }
    public string ProviderResponseId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = AiConsumptionStatuses.Completed;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal InputPricePerMillionUsd { get; set; }
    public decimal OutputPricePerMillionUsd { get; set; }
    public decimal InputCostUsd { get; set; }
    public decimal OutputCostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AiResponseExecution ResponseExecution { get; set; } = null!;
}

public sealed class AiModelPrice
{
    public long Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public decimal InputPricePerMillionUsd { get; set; }
    public decimal OutputPricePerMillionUsd { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AiConsumptionSettings
{
    public int Id { get; set; } = 1;
    public string Model { get; set; } = string.Empty;
    public long DefaultMonthlyTokenLimit { get; set; }
    public decimal DefaultAlertPercentage { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AiUserLimit
{
    public long UserId { get; set; }
    public long? MonthlyTokenLimit { get; set; }
    public decimal? AlertPercentage { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public AppUser User { get; set; } = null!;
}

public sealed class AiConsumptionAlert
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public DateOnly PeriodMonth { get; set; }
    public string Level { get; set; } = string.Empty;
    public long ConsumedTokens { get; set; }
    public long TokenLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public AppUser User { get; set; } = null!;
}

public static class AiConsumptionStatuses
{
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}

public static class AiConsumptionAlertLevels
{
    public const string Warning = "WARNING";
    public const string LimitReached = "LIMIT_REACHED";
}
