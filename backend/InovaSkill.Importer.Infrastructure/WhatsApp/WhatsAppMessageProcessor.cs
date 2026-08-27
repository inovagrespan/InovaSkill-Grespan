using System.Collections.Concurrent;
using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Application.WhatsApp;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public sealed class WhatsAppMessageProcessor(
    ImportDbContext db,
    IWhatsAppGateway gateway,
    IAudioTranscriptionService transcriptionService,
    BusinessAssistantService assistant,
    AiConsumptionService consumptionService,
    IOptions<WhatsAppOptions> whatsAppOptions,
    IOptions<AssistantOptions> assistantOptions) : IOperationalJobProcessor
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ConversationGates = new();

    public string JobType => WhatsAppJobCodes.MessageProcessing;

    public async Task ProcessAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await db.WhatsAppMessageReceipts
            .Include(x => x.WhatsAppUserLink).ThenInclude(x => x!.User)
            .SingleAsync(x => x.Id == receiptId, cancellationToken);
        if (IsTerminalWithoutProcessing(receipt.Status)) return;
        var link = receipt.WhatsAppUserLink;
        var user = link?.User;
        if (link is null || user is null || link.Status != WhatsAppUserLinkStatuses.Active)
            throw new InvalidOperationException("O vínculo do WhatsApp não está ativo.");

        var conversationGate = ConversationGates.GetOrAdd(link.Id, static _ => new SemaphoreSlim(1, 1));
        await conversationGate.WaitAsync(cancellationToken);
        try { await ProcessSerializedAsync(receipt, link, user, cancellationToken); }
        finally { conversationGate.Release(); }
    }

    private async Task ProcessSerializedAsync(
        WhatsAppMessageReceipt receipt,
        WhatsAppUserLink link,
        AppUser user,
        CancellationToken cancellationToken)
    {
        await db.Entry(receipt).ReloadAsync(cancellationToken);
        if (IsTerminalWithoutProcessing(receipt.Status)) return;

        if (receipt.Status == WhatsAppMessageStatuses.RateLimitNotice)
        {
            var cooldownSeconds = Math.Max(1, whatsAppOptions.Value.FloodCooldownSeconds);
            var notice = await gateway.SendTextAsync(
                link.NormalizedPhone,
                $"⏳ Recebi várias mensagens em sequência. Vou concluir o que já está em processamento. Aguarde {cooldownSeconds} segundos antes de enviar outra pergunta.",
                cancellationToken);
            receipt.ProviderOutboundMessageId = notice.ProviderMessageId;
            receipt.Status = WhatsAppMessageStatuses.RateLimitNoticeSent;
            receipt.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (receipt.ChatMessageId.HasValue)
        {
            var persistedAnswer = await db.ChatMessages.AsNoTracking()
                .Where(x => x.Id == receipt.ChatMessageId && x.Role == "assistant")
                .Select(x => x.Content).SingleAsync(cancellationToken);
            var retriedSend = await gateway.SendTextAsync(
                link.NormalizedPhone,
                WhatsAppAnswerFormatter.Format(persistedAnswer),
                cancellationToken);
            receipt.ProviderOutboundMessageId = retriedSend.ProviderMessageId;
            receipt.Status = WhatsAppMessageStatuses.Completed;
            receipt.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var question = receipt.TextContent;
        if (receipt.MessageType == "text" && receipt.Status == WhatsAppMessageStatuses.Received)
        {
            var aggregationDelay = TimeSpan.FromMilliseconds(Math.Max(0, whatsAppOptions.Value.MessageAggregationMilliseconds));
            var remainingDelay = receipt.CreatedAt.Add(aggregationDelay) - DateTime.UtcNow;
            if (remainingDelay > TimeSpan.Zero) await Task.Delay(remainingDelay, cancellationToken);

            var aggregationStart = receipt.CreatedAt.Subtract(aggregationDelay);
            var aggregationEnd = receipt.CreatedAt.Add(aggregationDelay);
            var candidates = await db.WhatsAppMessageReceipts
                .Where(x => x.WhatsAppUserLinkId == link.Id &&
                            x.Direction == WhatsAppMessageDirections.Inbound &&
                            x.MessageType == "text" &&
                            x.Status == WhatsAppMessageStatuses.Received &&
                            x.CreatedAt >= aggregationStart &&
                            x.CreatedAt <= aggregationEnd)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            if (candidates.Count > 0 && candidates[0].Id != receipt.Id) return;

            var selected = SelectAggregationBatch(candidates, assistantOptions.Value.MaximumQuestionLength);
            question = string.Join(" ", selected.Select(x => x.TextContent!.Trim()));
            foreach (var groupedReceipt in selected.Skip(1))
            {
                groupedReceipt.Status = WhatsAppMessageStatuses.Grouped;
                groupedReceipt.UpdatedAt = DateTime.UtcNow;
            }
        }

        receipt.Status = WhatsAppMessageStatuses.Processing;
        receipt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

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
            var sent = await gateway.SendTextAsync(link.NormalizedPhone, WhatsAppAnswerFormatter.Format(answer.Answer), cancellationToken);
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
        finally { await consumptionService.CompleteAsync(succeeded, CancellationToken.None); }
    }

    public static IReadOnlyList<WhatsAppMessageReceipt> SelectAggregationBatch(
        IReadOnlyList<WhatsAppMessageReceipt> candidates,
        int maximumQuestionLength)
    {
        var selected = new List<WhatsAppMessageReceipt>();
        var currentLength = 0;
        foreach (var candidate in candidates)
        {
            var text = candidate.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var nextLength = currentLength + (selected.Count == 0 ? 0 : 1) + text.Length;
            if (selected.Count > 0 && nextLength > Math.Max(1, maximumQuestionLength)) break;
            selected.Add(candidate);
            currentLength = nextLength;
        }
        return selected;
    }

    private static bool IsTerminalWithoutProcessing(string status) =>
        status is WhatsAppMessageStatuses.Completed or
            WhatsAppMessageStatuses.Grouped or
            WhatsAppMessageStatuses.RateLimited or
            WhatsAppMessageStatuses.RateLimitNoticeSent;
}
