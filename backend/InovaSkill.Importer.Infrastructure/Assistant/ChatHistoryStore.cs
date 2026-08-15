using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Assistant;

public interface IChatHistoryStore
{
    Task<ChatSessionSnapshot> LoadOrCreateAsync(
        Guid? sessionId,
        long userId,
        int maximumMessages,
        CancellationToken cancellationToken);

    Task<ChatSessionSnapshot> LoadOrCreateForChannelAsync(
        Guid? sessionId,
        long userId,
        string channel,
        Guid? whatsAppUserLinkId,
        int maximumMessages,
        CancellationToken cancellationToken) =>
        LoadOrCreateAsync(sessionId, userId, maximumMessages, cancellationToken);

    Task AppendAsync(
        Guid sessionId,
        string role,
        string content,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatSessionSummary>> ListAsync(
        long userId,
        int offset,
        int maximumSessions,
        CancellationToken cancellationToken);

    Task<ChatSessionHistory?> LoadAsync(
        Guid sessionId,
        long userId,
        int maximumMessages,
        CancellationToken cancellationToken);
}

public sealed record ChatSessionSnapshot(Guid SessionId, IReadOnlyList<ChatModelInputMessage> Messages);
public sealed record ChatSessionSummary(Guid SessionId, string Preview, DateTime UpdatedAt);
public sealed record ChatHistoryMessage(Guid Id, string Role, string Content, DateTime CreatedAt);
public sealed record ChatSessionHistory(Guid SessionId, IReadOnlyList<ChatHistoryMessage> Messages);

public sealed class ChatHistoryStore(ImportDbContext dbContext) : IChatHistoryStore
{
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";

    public async Task<ChatSessionSnapshot> LoadOrCreateAsync(
        Guid? sessionId,
        long userId,
        int maximumMessages,
        CancellationToken cancellationToken)
        => await LoadOrCreateForChannelAsync(sessionId, userId, ChatSessionChannels.Web, null, maximumMessages, cancellationToken);

    public async Task<ChatSessionSnapshot> LoadOrCreateForChannelAsync(
        Guid? sessionId,
        long userId,
        string channel,
        Guid? whatsAppUserLinkId,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        ChatSession? session = null;
        if (sessionId.HasValue)
        {
            session = await dbContext.ChatSessions
                .FirstOrDefaultAsync(
                    item => item.Id == sessionId.Value && item.UserId == userId && item.Channel == channel,
                    cancellationToken);
        }

        if (session is null)
        {
            var now = DateTime.UtcNow;
            session = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Channel = channel,
                WhatsAppUserLinkId = whatsAppUserLinkId,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.ChatSessions.Add(session);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var messages = await dbContext.ChatMessages.AsNoTracking()
            .Where(message => message.ChatSessionId == session.Id)
            .OrderByDescending(message => message.CreatedAt)
            .Take(maximumMessages)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ChatModelInputMessage(message.Role, message.Content))
            .ToListAsync(cancellationToken);

        return new ChatSessionSnapshot(session.Id, messages);
    }

    public async Task AppendAsync(
        Guid sessionId,
        string role,
        string content,
        CancellationToken cancellationToken)
    {
        if (role is not UserRole and not AssistantRole)
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Papel de mensagem inválido.");
        }

        var now = DateTime.UtcNow;
        dbContext.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            Role = role,
            Content = content,
            CreatedAt = now
        });

        await dbContext.ChatSessions
            .Where(session => session.Id == sessionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.UpdatedAt, now),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatSessionSummary>> ListAsync(
        long userId,
        int offset,
        int maximumSessions,
        CancellationToken cancellationToken) =>
        await dbContext.ChatSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.Channel == ChatSessionChannels.Web)
            .OrderByDescending(session => session.UpdatedAt)
            .ThenByDescending(session => session.Id)
            .Skip(offset)
            .Take(maximumSessions)
            .Select(session => new ChatSessionSummary(
                session.Id,
                session.Messages
                    .Where(message => message.Role == UserRole)
                    .OrderBy(message => message.CreatedAt)
                    .Select(message => message.Content)
                    .FirstOrDefault() ?? "Nova conversa",
                session.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<ChatSessionHistory?> LoadAsync(
        Guid sessionId,
        long userId,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ChatSessions.AsNoTracking()
            .AnyAsync(session => session.Id == sessionId && session.UserId == userId && session.Channel == ChatSessionChannels.Web, cancellationToken);
        if (!exists) return null;

        var messages = await dbContext.ChatMessages.AsNoTracking()
            .Where(message => message.ChatSessionId == sessionId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(maximumMessages)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ChatHistoryMessage(
                message.Id,
                message.Role,
                message.Content,
                message.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ChatSessionHistory(sessionId, messages);
    }
}
