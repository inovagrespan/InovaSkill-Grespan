using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class StaticTollCatalog : ITollCatalog
{
    public const decimal MatchRadiusMeters = 1500m;
    private const decimal EarthRadiusMeters = 6371000m;
    private const decimal AutomaticDiscountRate = 0.05m;

    public TollCatalogDefinition Current { get; } = new(
        "BR-SP-2026-06-04-v1",
        null,
        BuildPlazas());

    public RouteTollEstimate Estimate(IReadOnlyList<IReadOnlyList<decimal>> geometry, int axleCount)
    {
        if (axleCount is < RouteCostPolicy.MinimumAxleCount or > RouteCostPolicy.MaximumAxleCount)
            throw new ArgumentOutOfRangeException(nameof(axleCount));
        if (geometry.Count == 0) return new RouteTollEstimate([], 0, 0);

        var passages = Current.Plazas.Select(plaza => (plaza, count: CountPassages(geometry, plaza)))
            .Where(item => item.count > 0)
            .Select(item =>
            {
                var manual = item.plaza.CommercialManualTariffsByAxle[axleCount];
                var automatic = RouteCostPolicy.RoundCurrency(manual * (1 - item.plaza.AutomaticDiscountRate));
                return new TollPassageEstimate(item.plaza, axleCount, item.count, automatic,
                    RouteCostPolicy.RoundCurrency(automatic * item.count));
            }).ToArray();
        return new RouteTollEstimate(passages, passages.Sum(item => item.Passages),
            RouteCostPolicy.RoundCurrency(passages.Sum(item => item.TotalCost)));
    }

    private static int CountPassages(IReadOnlyList<IReadOnlyList<decimal>> geometry, TollPlazaDefinition plaza)
    {
        var passages = 0;
        var wasNear = false;
        for (var index = 0; index < geometry.Count; index++)
        {
            var current = geometry[index];
            var previous = index == 0 ? current : geometry[index - 1];
            if (current.Count < 2 || previous.Count < 2) continue;
            var near = DistanceToSegmentMeters(plaza.Longitude, plaza.Latitude,
                previous[0], previous[1], current[0], current[1]) <= MatchRadiusMeters;
            if (near && !wasNear) passages++;
            wasNear = near;
        }
        return passages;
    }

    private static decimal DistanceToSegmentMeters(decimal longitude, decimal latitude,
        decimal startLongitude, decimal startLatitude, decimal endLongitude, decimal endLatitude)
    {
        var latitudeRadians = (double)latitude * Math.PI / 180d;
        var scaleX = Math.Cos(latitudeRadians) * (double)EarthRadiusMeters * Math.PI / 180d;
        var scaleY = (double)EarthRadiusMeters * Math.PI / 180d;
        var px = (double)longitude * scaleX;
        var py = (double)latitude * scaleY;
        var sx = (double)startLongitude * scaleX;
        var sy = (double)startLatitude * scaleY;
        var ex = (double)endLongitude * scaleX;
        var ey = (double)endLatitude * scaleY;
        var dx = ex - sx;
        var dy = ey - sy;
        var lengthSquared = dx * dx + dy * dy;
        var projection = lengthSquared == 0 ? 0 : Math.Clamp(((px - sx) * dx + (py - sy) * dy) / lengthSquared, 0, 1);
        return (decimal)Math.Sqrt(Math.Pow(px - (sx + projection * dx), 2) + Math.Pow(py - (sy + projection * dy), 2));
    }

    private static IReadOnlyList<TollPlazaDefinition> BuildPlazas() =>
    [
        Eixo("P01", "Rio Claro", "SP-310", 181.5m, "Rio Claro", -22.3740609m, -47.620447m, [23.3m,35m,46.6m,58.3m,69.9m,81.5m,93.2m,104.9m]),
        Eixo("P02", "Itirapina", "SP-310", 217m, "Itirapina", -22.1289932m, -47.8080853m, [14.1m,21.1m,28.2m,35.2m,42.3m,49.4m,56.4m,63.4m]),
        Eixo("P03", "Brotas", "SP-225", 106.9m, "Brotas", -22.263813m, -47.9025024m, [20.1m,30.1m,40.1m,50.1m,60.2m,70.2m,80.2m,90.3m]),
        Eixo("P04", "Dois Córregos", "SP-225", 143.8m, "Dois Córregos", -22.2559811m, -48.2438122m, [22.8m,34.1m,45.5m,56.9m,68.3m,79.7m,91.1m,102.4m]),
        Eixo("P05", "Jaú", "SP-225", 99.4m, "Jaú", -22.3182804m, -48.7254956m, [29.3m,43.9m,58.6m,73.2m,87.9m,102.5m,117.2m,131.8m]),
        Eixo("P06", "Piracicaba", "SP-308", 182.25m, "Piracicaba", -22.606497m, -47.7148423m, [14.7m,22.1m,29.5m,36.9m,44.2m,51.6m,59m,66.4m]),
        Eixo("P07", "São Pedro I", "SP-304", 183.4m, "São Pedro", -22.6482408m, -47.8071778m, [16.5m,24.8m,33.1m,41.4m,49.6m,57.9m,66.2m,74.4m]),
        Eixo("P08", "São Pedro II", "SP-304", 215.1m, "São Pedro", -22.5648998m, -48.0620089m, [17.1m,25.7m,34.3m,42.9m,51.4m,60m,68.6m,77.2m]),
        Eixo("P09", "Torrinha", "SP-304", 155.8m, "Torrinha", -22.4050711m, -48.2616579m, [15.3m,22.9m,30.5m,38.2m,45.8m,53.5m,61.1m,68.7m]),
        Eixo("P10", "Piratininga", "SP-294", 370m, "Piratininga", -22.3456145m, -49.2862524m, [26m,39m,52m,65.1m,78.1m,91.1m,104.1m,117.1m]),
        Eixo("P11", "Garça", "SP-294", 425.7m, "Garça", -22.2138223m, -49.7424902m, [23.5m,35.2m,46.9m,58.7m,70.4m,82.2m,93.9m,105.6m]),
        Eixo("P12", "Oriente", "SP-294", 474.8m, "Oriente", -22.1388848m, -50.1180007m, [23.8m,35.8m,47.7m,59.6m,71.6m,83.5m,95.4m,107.3m]),
        Eixo("P13", "Parapuã", "SP-294", 551.5m, "Parapuã", -21.8420125m, -50.721259m, [22.9m,34.3m,45.7m,57.2m,68.6m,80m,91.5m,102.9m]),
        Eixo("P14", "Inúbia Paulista", "SP-294", 581.7m, "Inúbia Paulista", -21.7337511m, -50.9821736m, [15.9m,23.9m,31.8m,39.8m,47.8m,55.7m,63.7m,71.7m]),
        Eixo("P15", "Pacaembu", "SP-294", 623.1m, "Pacaembu", -21.5458248m, -51.3117108m, [17.7m,26.6m,35.5m,44.3m,53.2m,62.1m,70.9m,79.8m]),
        Eixo("P16", "Indiana", "SP-425", 436m, "Indiana", -22.143444m, -51.2370804m, [14.5m,21.7m,28.9m,36.2m,43.4m,50.6m,57.9m,65.1m]),
        Eixo("P17", "Paraguaçu Paulista", "SP-284", 458.3m, "Paraguaçu Paulista", -22.5723235m, -50.5172302m, [15.9m,23.9m,31.8m,39.8m,47.8m,55.7m,63.7m,71.7m]),
        Eixo("P18", "Rancharia", "SP-284", 531.2m, "Rancharia", -22.2186759m, -51.0208485m, [16.7m,25m,33.3m,41.6m,50m,58.3m,66.6m,75m]),
        Eixo("P19", "Martinópolis", "SP-425", 400.1m, "Martinópolis", -21.9592878m, -50.9552327m, [11.9m,17.8m,23.7m,29.7m,35.6m,41.5m,47.5m,53.4m]),
        Eixo("P20", "Santa Mercedes", "SP-294", 670.8m, "Santa Mercedes", -21.3613234m, -51.7129677m, [13.6m,20.4m,27.2m,34m,40.8m,47.6m,54.4m,61.2m]),
        Eixo("P21", "Cabrália Paulista", "SP-293", 2m, "Cabrália Paulista", -22.4953675m, -49.3126802m, [7.7m,11.6m,15.5m,19.3m,23.2m,27.1m,30.9m,34.8m]),
        Other("VR-AVAI", "Avaí", "ViaRondon", "SP-300", 367.7m, "Avaí", -22.16743m, -49.23975m, 8.5m),
        Other("VR-PIRAJUI", "Pirajuí", "ViaRondon", "SP-300", 400.8m, "Pirajuí", -21.95899m, -49.4663m, 8m),
        Other("VR-PROMISSAO", "Promissão", "ViaRondon", "SP-300", 455.7m, "Promissão", -21.62194m, -49.8529m, 9.6m),
        Other("TIE-AREIOPOLIS", "Areiópolis", "Rodovias do Tietê", "SP-300", 285m, "Areiópolis", -22.67581m, -48.68876m, 9m),
        Other("TIE-AGUDOS", "Agudos", "Rodovias do Tietê", "SP-300", 314m, "Agudos", -22.51336m, -48.9046m, 8.8m),
        Other("TBR-LINS", "Lins", "Triunfo Transbrasiliana", "BR-153", 183.8m, "Lins", -21.71103m, -49.8084m, 10.1m),
        Other("TBR-VERA-CRUZ", "Vera Cruz", "Triunfo Transbrasiliana", "BR-153", 268.1m, "Vera Cruz", -22.3363m, -49.89896m, 10.1m),
        Other("CART-PIRATININGA", "Piratininga", "CART", "SP-225", 251.9m, "Piratininga", -22.45084m, -49.1706m, 10.7m),
        Other("CART-SANTA-CRUZ", "Santa Cruz do Rio Pardo", "CART", "SP-225", 300.9m, "Santa Cruz do Rio Pardo", -22.74894m, -49.4784m, 10.4m),
        Other("ENT-MARILIA", "Marília", "Entrevias", "SP-333", 315.1m, "Marília", -22.0908m, -49.9095m, 13.6m),
        Other("ENT-ECHAPORA", "Echaporã", "Entrevias", "SP-333", 354.7m, "Echaporã", -22.3308m, -50.1199m, 11.8m),
        Other("VP-JAU", "Jaú", "Arteris ViaPaulista", "SP-255", 165.6m, "Jaú", -22.4083m, -48.5642m, 7m)
    ];

    private static TollPlazaDefinition Eixo(string code, string name, string highway, decimal kilometer,
        string municipality, decimal latitude, decimal longitude, IReadOnlyList<decimal> tariffs) =>
        new(code, name, "EIXO SP", highway, kilometer, municipality, latitude, longitude,
            AutomaticDiscountRate, tariffs.Select((tariff, index) => (axles: index + 2, tariff)).ToDictionary(x => x.axles, x => x.tariff));

    private static TollPlazaDefinition Other(string code, string name, string operatorName, string highway,
        decimal kilometer, string municipality, decimal latitude, decimal longitude, decimal baseTariff) =>
        new(code, name, operatorName, highway, kilometer, municipality, latitude, longitude,
            AutomaticDiscountRate, Enumerable.Range(2, 8).ToDictionary(axles => axles,
                axles => RouteCostPolicy.RoundCurrency(baseTariff * axles)));
}
