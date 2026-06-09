using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Auth;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Login_AcceptsUserName()
    {
        await using var db = CreateDbContext();
        var passwordHasher = new PasswordHasher<AppUser>();
        var user = CreateUser(passwordHasher);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db, passwordHasher);

        var result = await controller.Login(new LoginRequest("admin", "admin"), CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LoginResponse>(response.Value);
        var token = CreateTokenService().Validate(payload.Token);
        Assert.Equal(AppUserRoles.Admin, token?.FindFirst("role")?.Value);
    }

    [Fact]
    public async Task Login_AcceptsEmail()
    {
        await using var db = CreateDbContext();
        var passwordHasher = new PasswordHasher<AppUser>();
        var user = CreateUser(passwordHasher);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db, passwordHasher);

        var result = await controller.Login(new LoginRequest("admin@local.test", "admin"), CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<LoginResponse>(response.Value);
    }

    private static ImportDbContext CreateDbContext()
    {
        return new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static AppUser CreateUser(PasswordHasher<AppUser> passwordHasher)
    {
        var user = new AppUser
        {
            Name = "admin",
            Email = "admin@local.test",
            Role = AppUserRoles.Admin,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "admin");
        return user;
    }

    private static AuthController CreateController(ImportDbContext db, PasswordHasher<AppUser> passwordHasher)
    {
        return new AuthController(db, passwordHasher, CreateTokenService());
    }

    private static JwtTokenService CreateTokenService()
    {
        return new JwtTokenService(Options.Create(new JwtAuthOptions
        {
            Issuer = "Tests",
            Audience = "Frontend",
            Secret = "tests-secret-with-more-than-32-characters",
            ExpirationMinutes = 30
        }));
    }
}
