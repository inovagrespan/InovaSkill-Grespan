namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";
    public string BaseUrl { get; set; } = "http://localhost:8081";
    public string InstanceName { get; set; } = "grespan";
    public string WebhookSecret { get; set; } = string.Empty;
    public int VerificationCodeLifetimeMinutes { get; set; } = 10;
    public int MaximumVerificationAttempts { get; set; } = 5;
    public int GatewayTimeoutSeconds { get; set; } = 20;
    public int MaximumAudioBytes { get; set; } = 10 * 1024 * 1024;
    public int ConnectionPollingSeconds { get; set; } = 10;
    public int MessageAggregationMilliseconds { get; set; } = 1000;
    public int FloodWindowSeconds { get; set; } = 30;
    public int FloodMaximumMessages { get; set; } = 8;
    public int FloodCooldownSeconds { get; set; } = 30;
}
