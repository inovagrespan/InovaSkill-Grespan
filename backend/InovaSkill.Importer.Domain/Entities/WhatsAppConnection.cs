namespace InovaSkill.Importer.Domain.Entities;

public sealed class WhatsAppConnection
{
    public int Id { get; set; }
    public string InstanceName { get; set; } = string.Empty;
    public string Status { get; set; } = WhatsAppConnectionStatuses.Disconnected;
    public string? ConnectedPhone { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastEventAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class WhatsAppConnectionStatuses
{
    public const string Disconnected = "disconnected";
    public const string Connecting = "connecting";
    public const string Connected = "connected";
}
