using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Configuration;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class LocalImportFileStorage(IConfiguration configuration) : IImportFileStorage
{
    private const string DefaultStorageDirectory = "route-imports";

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        var storageKey = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);
        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = File.OpenRead(ResolvePath(storageKey));
        return Task.FromResult(stream);
    }

    private string ResolvePath(string storageKey)
    {
        var configuredPath = configuration["Storage:ImportsPath"];
        var root = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", DefaultStorageDirectory))
            : Path.GetFullPath(configuredPath);
        return Path.Combine(root, Path.GetFileName(storageKey));
    }
}
