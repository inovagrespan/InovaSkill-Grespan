using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Auth;

public sealed class DefaultUserSeederTests
{
    [Fact]
    public async Task EnsureDefaultUsersAsync_CreatesUsersExpectedByFrontend()
    {
        await using var db = CreateDbContext();
        var passwordHasher = new PasswordHasher<AppUser>();
        var seeder = new DefaultUserSeeder(db, passwordHasher);

        await seeder.EnsureDefaultUsersAsync();

        AssertSeededUser(db, passwordHasher, "rh", "rh", AppUserRoles.Admin);
        AssertSeededUser(db, passwordHasher, "admin_system", "admin_system", AppUserRoles.AdminSystem);
        AssertSeededUser(db, passwordHasher, "vendas", "vendas", AppUserRoles.Vendas);
        AssertSeededUser(db, passwordHasher, "logistica", "logistica", AppUserRoles.Logistica);
        AssertSeededUser(db, passwordHasher, "diretor", "diretor", AppUserRoles.Diretor);
    }

    [Fact]
    public async Task EnsureDefaultUsersAsync_RenamesLegacyAdminToRh()
    {
        await using var db = CreateDbContext();
        var passwordHasher = new PasswordHasher<AppUser>();
        var legacyAdmin = new AppUser
        {
            Name = "admin",
            Email = "admin@local.test",
            Role = AppUserRoles.Admin
        };
        legacyAdmin.PasswordHash = passwordHasher.HashPassword(legacyAdmin, "admin");
        db.AppUsers.Add(legacyAdmin);
        await db.SaveChangesAsync();

        await new DefaultUserSeeder(db, passwordHasher).EnsureDefaultUsersAsync();

        var user = db.AppUsers.Single(x => x.Name == "diretor");
        Assert.Equal("diretor@local.test", user.Email);
        Assert.Equal(AppUserRoles.Diretor, user.Role);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "diretor"));
        Assert.Equal(7, db.AppUsers.Count());
    }

    [Fact]
    public async Task EnsureDefaultUsersAsync_IsIdempotent()
    {
        await using var db = CreateDbContext();
        var seeder = new DefaultUserSeeder(db, new PasswordHasher<AppUser>());

        await seeder.EnsureDefaultUsersAsync();
        await seeder.EnsureDefaultUsersAsync();

        Assert.Equal(7, db.AppUsers.Count());
    }

    private static ImportDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
