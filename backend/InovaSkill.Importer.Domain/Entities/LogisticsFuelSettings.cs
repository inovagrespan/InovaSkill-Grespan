namespace InovaSkill.Importer.Domain.Entities;

public sealed class LogisticsFuelSettings
{
    public static readonly Guid CurrentSettingsId = Guid.Parse("8f5e6ee8-3865-4be7-8ec7-b891f3656686");
    public const decimal DefaultDieselPricePerLiter = 6.90m;

    public Guid Id { get; set; } = CurrentSettingsId;
    public decimal DieselPricePerLiter { get; set; } = DefaultDieselPricePerLiter;
    public DateTimeOffset UpdatedAt { get; set; }
}
