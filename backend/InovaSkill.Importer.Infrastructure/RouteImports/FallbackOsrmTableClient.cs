using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class FallbackOsrmTableClient(
    OpenRouteServiceTableClient primary,
    OsrmTableClient fallback,
    ILogger<FallbackOsrmTableClient> logger) : IOsrmTableClient
{
    public async Task<OsrmTableResult> GetTableAsync(
        OsrmTableRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await primary.GetTableAsync(request, cancellationToken);
        }
        catch (OsrmTableException primaryException)
        {
            logger.LogWarning(primaryException,
                "OpenRouteService não calculou a matriz; iniciando fallback pelo OSRM.");
            try
            {
                return await fallback.GetTableAsync(request, cancellationToken);
            }
            catch (Exception fallbackException) when (fallbackException is OsrmTableException or HttpRequestException)
            {
                throw new OsrmTableException(
                    $"OpenRouteService falhou ({primaryException.Message}) e o fallback OSRM também falhou ({fallbackException.Message}).",
                    new AggregateException(primaryException, fallbackException));
            }
        }
    }

    public async Task<bool> IsHealthyAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken) =>
        await primary.IsHealthyAsync(latitude, longitude, cancellationToken) ||
        await fallback.IsHealthyAsync(latitude, longitude, cancellationToken);
}
