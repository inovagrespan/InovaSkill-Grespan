using System.Security.Claims;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
public sealed class RouteOptimizationRunsController(IRouteOptimizationService optimizationService) : ControllerBase
{
    [HttpPost("api/route-optimization-runs")]
    public async Task<ActionResult<RouteOptimizationRunDto>> Start(
        [FromBody] StartRouteOptimizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Scope == RouteOptimizationScope.SingleRoute && request.RouteId is null)
        {
            return BadRequest(new { message = "routeId é obrigatório para otimização de uma rota." });
        }

        if (request.Scope == RouteOptimizationScope.AllRoutes && request.RouteId is not null)
        {
            return BadRequest(new { message = "routeId deve ser nulo para otimização global." });
        }

        var run = await optimizationService.StartOptimizationAsync(
            new RouteOptimizationStartRequest(
                request.Scope,
                request.ReferenceDate,
                request.RouteId,
                RouteOptimizationRequestedFrom.RouteScreen,
                ReadUserId()),
            cancellationToken);

        return AcceptedAtAction(nameof(Get), new { id = run.Id }, run);
    }

    [HttpPost("api/routes/{routeId:guid}/optimization-runs")]
    public ActionResult<RouteOptimizationRunDto> StartForRoute(
        Guid routeId,
        [FromBody] StartSingleRouteOptimizationRequest request)
    {
        return BadRequest(new
        {
            message = "A otimização manual por rota foi substituída pela otimização global. Use POST /api/route-optimization-runs com scope AllRoutes."
        });
    }

    [HttpGet("api/route-optimization-runs/latest")]
    public async Task<ActionResult<RouteOptimizationRunDto>> GetLatest(
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        var run = await optimizationService.GetLatestGlobalOptimizationAsync(referenceDate, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("api/route-optimization-runs/{id:guid}")]
    public async Task<ActionResult<RouteOptimizationRunDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var run = await optimizationService.GetOptimizationResultAsync(id, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("api/routes/{routeId:guid}/latest-optimization")]
    public async Task<ActionResult<RouteLatestOptimizationDto>> GetLatestForRoute(
        Guid routeId,
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        return Ok(await optimizationService.GetLatestRouteOptimizationAsync(routeId, referenceDate, cancellationToken));
    }

    private long ReadUserId()
    {
        var value = User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : 0;
    }
}

public sealed record StartRouteOptimizationRequest(
    RouteOptimizationScope Scope,
    DateOnly ReferenceDate,
    Guid? RouteId);

public sealed record StartSingleRouteOptimizationRequest(DateOnly ReferenceDate);
