using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Domain.Entities;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Auth;

public sealed class JwtTokenService(IOptions<JwtAuthOptions> options)
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);
    private readonly JwtAuthOptions options = options.Value;

    public string Generate(AppUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(options.ExpirationMinutes);
        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };
        var payload = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["name"] = user.Name,
            ["email"] = user.Email,
            ["role"] = user.Role,
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds()
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = Sign($"{encodedHeader}.{encodedPayload}");

        return $"{encodedHeader}.{encodedPayload}.{signature}";
    }

    public ClaimsPrincipal? Validate(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        var expectedSignature = Sign($"{parts[0]}.{parts[1]}");
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedSignature),
                Encoding.ASCII.GetBytes(parts[2])))
        {
            return null;
        }

        Dictionary<string, JsonElement>? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlDecode(parts[1]));
        }
        catch
        {
            return null;
        }

        if (payload is null ||
            !HasStringClaim(payload, "iss", options.Issuer) ||
            !HasStringClaim(payload, "aud", options.Audience) ||
            !payload.TryGetValue("exp", out var expElement) ||
            !expElement.TryGetInt64(out var exp))
        {
            return null;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
        if (expiresAt <= DateTimeOffset.UtcNow.Subtract(ClockSkew)) return null;

        var claims = payload
            .Where(item => item.Value.ValueKind == JsonValueKind.String)
            .Select(item => new Claim(item.Key, item.Value.GetString() ?? string.Empty))
            .ToList();

        var role = claims.FirstOrDefault(x => x.Type == "role")?.Value;
        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private string Sign(string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Secret));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.ASCII.GetBytes(value)));
    }

    private static bool HasStringClaim(IReadOnlyDictionary<string, JsonElement> payload, string name, string expected)
    {
        return payload.TryGetValue(name, out var value) &&
               value.ValueKind == JsonValueKind.String &&
               string.Equals(value.GetString(), expected, StringComparison.Ordinal);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
