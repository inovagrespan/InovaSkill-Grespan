using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Application.WhatsApp;

namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public sealed class WhatsAppMessageQueue(IOperationalJobQueue queue) : IWhatsAppMessageQueue
{
    public Task<Guid?> TryQueueAsync(Guid receiptId, CancellationToken cancellationToken) =>
        queue.TryQueueAsync(WhatsAppJobCodes.MessageProcessing, receiptId, cancellationToken);
}
