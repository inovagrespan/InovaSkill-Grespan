using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Auth;

public sealed class DefaultUserSeederTests
{
    [Fact]
    public async Task EnsureDefaultUsersAsync_CreatesAreaManagersDirectorAndRhAdminWithMatchingPasswords()
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

        Assert.False(await db.AppUsers.AnyAsync(x => x.Name == "admin" || x.Email == "admin@local.test"));
        AssertSeededUser(db, passwordHasher, "rh", "rh", AppUserRoles.Admin);
    }

    private static ImportDbContext CreateDbContext()
    {
        return new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static void AssertSeededUser(
        ImportDbContext db,
        PasswordHasher<AppUser> passwordHasher,
        string userName,
        string password,
        string expectedRole)
    {
        var user = db.AppUsers.Single(x => x.Name == userName);

        Assert.Equal($"{userName}@local.test", user.Email);
        Assert.Equal(expectedRole, user.Role);
        Assert.NotEqual(PasswordVerificationResult.Failed, passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password));
    }
}
