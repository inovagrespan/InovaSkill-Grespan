using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Application.WhatsApp;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public sealed class WhatsAppMessageProcessor(
    ImportDbContext db,
    IWhatsAppGateway gateway,
    IAudioTranscriptionService transcriptionService,
    BusinessAssistantService assistant,
    AiConsumptionService consumptionService) : IOperationalJobProcessor
{
    public string JobType => WhatsAppJobCodes.MessageProcessing;

    public async Task ProcessAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await db.WhatsAppMessageReceipts
            .Include(x => x.WhatsAppUserLink).ThenInclude(x => x!.User)
            .SingleAsync(x => x.Id == receiptId, cancellationToken);
        if (receipt.Status == WhatsAppMessageStatuses.Completed) return;
        var link = receipt.WhatsAppUserLink;
        var user = link?.User;
        if (link is null || user is null || link.Status != WhatsAppUserLinkStatuses.Active)
            throw new InvalidOperationException("O vínculo do WhatsApp não está ativo.");

        if (receipt.ChatMessageId.HasValue)
        {
            var persistedAnswer = await db.ChatMessages.AsNoTracking()
                .Where(x => x.Id == receipt.ChatMessageId && x.Role == "assistant")
                .Select(x => x.Content).SingleAsync(cancellationToken);
            var retriedSend = await gateway.SendTextAsync(link.NormalizedPhone, persistedAnswer, cancellationToken);
            receipt.ProviderOutboundMessageId = retriedSend.ProviderMessageId;
            receipt.Status = WhatsAppMessageStatuses.Completed;
            receipt.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        receipt.Status = WhatsAppMessageStatuses.Processing;
        receipt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var question = receipt.TextContent;
        if (receipt.MessageType == "audio")
        {
            await using var media = await gateway.DownloadMediaAsync(receipt.MediaReference ?? receipt.ProviderMessageId, cancellationToken);
            question = await transcriptionService.TranscribeAsync(media, $"{receipt.ProviderMessageId}.ogg", cancellationToken);
            receipt.TextContent = question;
            await db.SaveChangesAsync(cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(question)) throw new InvalidOperationException("A mensagem recebida não possui conteúdo processável.");

        var sessionId = await db.ChatSessions.AsNoTracking()
            .Where(x => x.WhatsAppUserLinkId == link.Id && x.Channel == ChatSessionChannels.WhatsApp)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        var admission = await consumptionService.BeginAsync(user.Id, user.Role, cancellationToken);
        if (!admission.Allowed)
        {
            await gateway.SendTextAsync(link.NormalizedPhone,
                "Seu limite mensal de uso do assistente foi atingido. Procure um administrador.", cancellationToken);
            receipt.Status = WhatsAppMessageStatuses.Completed;
            receipt.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var succeeded = false;
        try
        {
            var answer = await assistant.AnswerAsync(sessionId, question,
                new ChatExecutionContext(user.Id, user.Role), cancellationToken,
                ChatSessionChannels.WhatsApp, link.Id);
            receipt.ChatSessionId = answer.SessionId;
            receipt.ChatMessageId = await db.ChatMessages.AsNoTracking()
                .Where(x => x.ChatSessionId == answer.SessionId && x.Role == "assistant")
                .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            var sent = await gateway.SendTextAsync(link.NormalizedPhone, answer.Answer, cancellationToken);
            receipt.ProviderOutboundMessageId = sent.ProviderMessageId;
            receipt.Status = WhatsAppMessageStatuses.Completed;
            receipt.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            succeeded = true;
        }
        catch
        {
            receipt.Status = WhatsAppMessageStatuses.Failed;
            receipt.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await consumptionService.CompleteAsync(succeeded, CancellationToken.None);
        }
    }
}
