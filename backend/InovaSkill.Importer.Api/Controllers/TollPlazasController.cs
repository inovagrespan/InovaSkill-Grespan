using InovaSkill.Importer.Application.RouteImports;
using Microsoft.AspNetCore.Mvc;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/logistics/toll-plazas")]
public sealed class TollPlazasController(ITollCatalog tollCatalog) : ControllerBase
{
    [HttpGet]
    public ActionResult Get()
    {
        var catalog = tollCatalog.Current;
        return Ok(new
        {
            catalog.Version,
            catalog.EffectiveFrom,
            items = catalog.Plazas.Select(plaza => new
            {
                plaza.Code,
                plaza.Name,
                plaza.OperatorName,
                plaza.Highway,
                plaza.Kilometer,
                plaza.Municipality,
                plaza.Latitude,
                plaza.Longitude,
                plaza.AutomaticDiscountRate,
                plaza.CommercialManualTariffsByAxle
            })
        });
    }
}
