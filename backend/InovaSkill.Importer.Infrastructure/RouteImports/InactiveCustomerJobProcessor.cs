using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Detection;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class InactiveCustomerJobProcessor(
    InactiveCustomerDetector detector) : IOperationalJobProcessor
{
    public string JobType => OperationalJobCodes.InactiveCustomerDetection;

    public async Task ProcessAsync(
        Guid relatedEntityId,
        CancellationToken cancellationToken)
    {
        var context = new DetectionContext(
            DetectionRunId: Guid.Empty,
            DetectorDefinitionId: Guid.Empty,
            DetectorCode: DetectorCodes.InactiveCustomer,
            ReferenceTime: DateTime.UtcNow);

        await detector.DetectAsync(context, cancellationToken);
    }
}
