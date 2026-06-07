namespace InovaSkill.Importer.Api.Auth;

public sealed class JwtAuthOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "InovaSkill";
    public string Audience { get; set; } = "InovaSkill.Frontend";
    public string Secret { get; set; } = "dev-secret-change-me-with-at-least-32-chars";
    public int ExpirationMinutes { get; set; } = 480;
}
