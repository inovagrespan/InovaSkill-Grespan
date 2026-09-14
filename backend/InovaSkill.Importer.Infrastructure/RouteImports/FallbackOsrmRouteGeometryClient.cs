using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class FallbackOsrmRouteGeometryClient(
    OpenRouteServiceRouteGeometryClient primary,
    OsrmRouteClient fallback,
    ILogger<FallbackOsrmRouteGeometryClient> logger) : IRouteGeometryClient
{
    public async Task<RouteGeometryResult> GetRouteAsync(
        IReadOnlyList<RouteGeometryPoint> points,
        CancellationToken cancellationToken)
    {
        try
        {
            return await primary.GetRouteAsync(points, cancellationToken);
        }
        catch (RouteGeometryException primaryException)
        {
            logger.LogWarning(primaryException,
                "OpenRouteService não calculou a geometria; iniciando fallback pelo OSRM.");
            try
            {
                return await fallback.GetRouteAsync(points, cancellationToken);
            }
            catch (Exception fallbackException) when (fallbackException is RouteGeometryException or HttpRequestException)
            {
                throw new RouteGeometryException(
                    $"OpenRouteService falhou ({primaryException.Message}) e o fallback OSRM também falhou ({fallbackException.Message}).",
                    new AggregateException(primaryException, fallbackException));
            }
        }
    }
}
