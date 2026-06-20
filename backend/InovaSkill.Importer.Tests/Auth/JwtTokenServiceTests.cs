using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Domain.Entities;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Auth;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void Generate_CreatesTokenAcceptedByValidate()
    {
        var service = CreateService(expirationMinutes: 30);

        var token = service.Generate(new AppUser { Id = 7, Name = "Ana Silva", Email = "ana@empresa.com", Role = AppUserRoles.Admin });
        var principal = service.Validate(token);

        Assert.NotNull(principal);
        Assert.Equal("7", principal.FindFirst("sub")?.Value);
        Assert.Equal("ana@empresa.com", principal.FindFirst("email")?.Value);
        Assert.Equal(AppUserRoles.Admin, principal.FindFirst("role")?.Value);
        Assert.Equal(AppUserRoles.Admin, principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
    }

    [Fact]
    public void Validate_RejectsTamperedToken()
    {
        var service = CreateService(expirationMinutes: 30);
        var token = service.Generate(new AppUser { Id = 7, Name = "Ana Silva", Email = "ana@empresa.com", Role = AppUserRoles.Admin });
        var tampered = $"{token[..^1]}{(token[^1] == 'a' ? 'b' : 'a')}";

        Assert.Null(service.Validate(tampered));
    }

    [Fact]
    public void Validate_RejectsExpiredToken()
    {
        var service = CreateService(expirationMinutes: -10);
        var token = service.Generate(new AppUser { Id = 7, Name = "Ana Silva", Email = "ana@empresa.com", Role = AppUserRoles.Admin });

        Assert.Null(service.Validate(token));
    }

    private static JwtTokenService CreateService(int expirationMinutes)
    {
        return new JwtTokenService(Options.Create(new JwtAuthOptions
        {
            Issuer = "Tests",
            Audience = "Frontend",
            Secret = "tests-secret-with-more-than-32-characters",
            ExpirationMinutes = expirationMinutes
        }));
    }
}
