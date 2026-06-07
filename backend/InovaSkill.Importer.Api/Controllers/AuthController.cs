using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
public sealed class AuthController(
    ImportDbContext dbContext,
    PasswordHasher<AppUser> passwordHasher,
    JwtTokenService jwtTokenService) : ControllerBase
{
    private const int MinimumPasswordLength = 6;

    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
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

        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.Name.Trim();
        var normalizedUserName = userName.ToLowerInvariant();
        var exists = await dbContext.AppUsers.AnyAsync(
            x => x.Email == email || x.Name.ToLower() == normalizedUserName,
            cancellationToken);
        if (exists)
        {
            return Conflict(Problem("Já existe um usuário cadastrado com este e-mail ou nome de usuário."));
        }

        var user = new AppUser
        {
            Name = userName,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created("/login", null);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(Problem("Informe usuário/e-mail e senha."));
        }

        var userOrEmail = request.UserOrEmail.Trim().ToLowerInvariant();
        var user = await dbContext.AppUsers.FirstOrDefaultAsync(
            x => x.Email == userOrEmail || x.Name.ToLower() == userOrEmail,
            cancellationToken);

        if (user is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(Problem("Usuário/e-mail ou senha inválidos."));
        }

        return Ok(new LoginResponse(jwtTokenService.Generate(user)));
    }

    private static ProblemDetails Problem(string detail)
    {
        return new ProblemDetails { Detail = detail };
    }
}
