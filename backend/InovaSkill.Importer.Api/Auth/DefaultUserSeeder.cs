using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Auth;

public sealed class DefaultUserSeeder(ImportDbContext db, PasswordHasher<AppUser> passwordHasher)
{
    public static readonly IReadOnlyList<SeedUser> DefaultUsers =
    [
        new("admin", "admin@local.test", "admin", AppUserRoles.Admin),
        new("admin_system", "admin_system@local.test", "admin_system", AppUserRoles.AdminSystem),
        new("vendas", "vendas@local.test", "vendas", AppUserRoles.Vendas),
        new("logistica", "logistica@local.test", "logistica", AppUserRoles.Logistica),
        new("diretor", "diretor@local.test", "diretor", AppUserRoles.Diretor)
    ];

    public async Task EnsureDefaultUsersAsync(CancellationToken cancellationToken = default)
    {
        foreach (var seed in DefaultUsers)
        {
            var normalizedName = seed.Name.ToLowerInvariant();
            var normalizedEmail = seed.Email.ToLowerInvariant();
            var existingUser = await db.AppUsers.FirstOrDefaultAsync(
                x => x.Email == normalizedEmail || x.Name.ToLower() == normalizedName,
                cancellationToken);

            if (existingUser is null)
            {
                var createdUser = new AppUser
                {
                    Name = seed.Name,
                    Email = normalizedEmail,
                    Role = seed.Role,
                    CreatedAt = DateTime.UtcNow
                };
                createdUser.PasswordHash = passwordHasher.HashPassword(createdUser, seed.Password);
                db.AppUsers.Add(createdUser);
                continue;
            }

            existingUser.Name = seed.Name;
            existingUser.Email = normalizedEmail;
            existingUser.Role = seed.Role;

            var verification = passwordHasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, seed.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, seed.Password);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

}

public sealed record SeedUser(string Name, string Email, string Password, string Role);
