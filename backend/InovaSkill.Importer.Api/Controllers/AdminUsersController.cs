using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
public sealed class AdminUsersController(
    ImportDbContext dbContext,
    PasswordHasher<AppUser> passwordHasher) : ControllerBase
{
    private const int MinimumPasswordLength = 6;

    private static readonly HashSet<string> AssignableRoles =
    [
        AppUserRoles.Diretor,
        AppUserRoles.Vendas,
        AppUserRoles.Logistica,
        AppUserRoles.Admin,
        AppUserRoles.AdminSystem
    ];

    [HttpPost]
    public async Task<ActionResult> Create(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Role) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(Problem("Preencha todos os campos obrigatórios."));
        }

        if (request.Password.Length < MinimumPasswordLength)
        {
            return BadRequest(Problem($"A senha deve ter pelo menos {MinimumPasswordLength} caracteres."));
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(Problem("A confirmação de senha não confere."));
        }

        var role = request.Role.Trim().ToLowerInvariant();
        if (!AssignableRoles.Contains(role))
        {
            return BadRequest(Problem("Selecione um perfil de acesso válido."));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.Name.Trim();
        var normalizedUserName = userName.ToLowerInvariant();
        var exists = await dbContext.AppUsers.AnyAsync(
            user => user.Email == email || user.Name.ToLower() == normalizedUserName,
            cancellationToken);
        if (exists)
        {
            return Conflict(Problem("Já existe um usuário cadastrado com este e-mail ou nome de usuário."));
        }

        var user = new AppUser
        {
            Name = userName,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/admin/users/{user.Id}", new { user.Id, user.Name, user.Email, user.Role });
    }

    private static ProblemDetails Problem(string detail) => new() { Detail = detail };
}
