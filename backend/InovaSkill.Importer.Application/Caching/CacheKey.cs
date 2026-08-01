using System.Security.Cryptography;
using System.Text;

namespace InovaSkill.Importer.Application.Caching;

public static class CacheKey
{
    private const int MaximumReadableKeyLength = 200;
    private const string Prefix = "inovaskill";

    public static string Create(string area, string version, params object?[] parts)
    {
        if (string.IsNullOrWhiteSpace(area)) throw new ArgumentException("A área do cache é obrigatória.", nameof(area));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("A versão do cache é obrigatória.", nameof(version));

        var segments = new[] { Prefix, Normalize(area), Normalize(version) }
            .Concat(parts.Select(part => Normalize(part?.ToString() ?? "null")));
        var readableKey = string.Join(':', segments);
        if (readableKey.Length <= MaximumReadableKeyLength) return readableKey;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(readableKey))).ToLowerInvariant();
        return $"{Prefix}:{Normalize(area)}:{Normalize(version)}:sha256:{hash}";
    }

    private static string Normalize(string value) => Uri.EscapeDataString(value.Trim().ToLowerInvariant());
}
