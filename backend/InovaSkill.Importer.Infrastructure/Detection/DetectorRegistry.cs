using InovaSkill.Importer.Application.Detection;

namespace InovaSkill.Importer.Infrastructure.Detection;

public sealed class DetectorRegistry(IEnumerable<IDetector> detectors) : IDetectorRegistry
{
    private readonly Dictionary<string, IDetector> _detectors = detectors.ToDictionary(
        d => d.Code.ToUpperInvariant(),
        d => d,
        StringComparer.OrdinalIgnoreCase);

    public IDetector Get(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Código do detector não pode ser vazio.", nameof(code));

        if (!_detectors.TryGetValue(code, out var detector))
            throw new InvalidOperationException(
                $"Nenhum detector registrado para o código '{code}'.");

        return detector;
    }
}
