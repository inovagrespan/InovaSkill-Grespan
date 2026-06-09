namespace InovaSkill.Importer.Domain.Entities;

public sealed class AppUser
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = AppUserRoles.Gestor;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AppUserRoles
{
    public const string Admin = "admin";
    public const string Gestor = "gestor";
}
