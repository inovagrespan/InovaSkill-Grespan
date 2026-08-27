namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public static class WhatsAppFloodPolicy
{
    public static WhatsAppFloodDecision Evaluate(
        DateTime now,
        DateTime? blockedUntil,
        int recentAcceptedMessages,
        int maximumMessages,
        TimeSpan cooldown)
    {
        if (blockedUntil > now)
            return new WhatsAppFloodDecision(false, false, blockedUntil);

        if (recentAcceptedMessages >= Math.Max(1, maximumMessages))
            return new WhatsAppFloodDecision(false, true, now.Add(cooldown));

        return new WhatsAppFloodDecision(true, false, null);
    }
}

public sealed record WhatsAppFloodDecision(bool Allowed, bool ShouldNotify, DateTime? BlockedUntil);
