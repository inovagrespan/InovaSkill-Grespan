using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task Create_PersistsNormalizedUserWithSelectedRole()
    {
        await using var db = CreateDbContext();
        var controller = new AdminUsersController(db, new PasswordHasher<AppUser>());

        var result = await controller.Create(
            new CreateAdminUserRequest("  maria  ", "  MARIA@EXAMPLE.COM ", AppUserRoles.Vendas, "segredo", "segredo"),
            CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        var user = await db.AppUsers.SingleAsync();
        Assert.Equal("maria", user.Name);
        Assert.Equal("maria@example.com", user.Email);
        Assert.Equal(AppUserRoles.Vendas, user.Role);
        Assert.NotEqual("segredo", user.PasswordHash);
    }

    [Theory]
    [InlineData("gestor")]
    [InlineData("perfil_inexistente")]
    public async Task Create_RejectsRoleWithoutFunctionalAccess(string role)
    {
        await using var db = CreateDbContext();
        var controller = new AdminUsersController(db, new PasswordHasher<AppUser>());

        var result = await controller.Create(
            new CreateAdminUserRequest("maria", "maria@example.com", role, "segredo", "segredo"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.AppUsers);
    }

    [Fact]
    public async Task Create_RejectsDuplicateNameOrEmail()
    {
        await using var db = CreateDbContext();
        db.AppUsers.Add(new AppUser { Name = "Maria", Email = "maria@example.com", PasswordHash = "hash" });
        await db.SaveChangesAsync();
        var controller = new AdminUsersController(db, new PasswordHasher<AppUser>());

        var result = await controller.Create(
            new CreateAdminUserRequest("MARIA", "outra@example.com", AppUserRoles.Diretor, "segredo", "segredo"),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.AppUsers);
    }

    [Fact]
    public async Task Create_RejectsMismatchedPasswordConfirmation()
    {
        await using var db = CreateDbContext();
        var controller = new AdminUsersController(db, new PasswordHasher<AppUser>());

        var result = await controller.Create(
            new CreateAdminUserRequest("maria", "maria@example.com", AppUserRoles.Logistica, "segredo", "diferente"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.AppUsers);
    }

    private static ImportDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
