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

    Task AppendAsync(
        Guid sessionId,
        string role,
        string content,
        CancellationToken cancellationToken);
}

public sealed record ChatSessionSnapshot(Guid SessionId, IReadOnlyList<ChatModelInputMessage> Messages);

public sealed class ChatHistoryStore(ImportDbContext dbContext) : IChatHistoryStore
{
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";

    public async Task<ChatSessionSnapshot> LoadOrCreateAsync(
        Guid? sessionId,
        long userId,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        ChatSession? session = null;
        if (sessionId.HasValue)
        {
            session = await dbContext.ChatSessions
                .FirstOrDefaultAsync(
                    item => item.Id == sessionId.Value && item.UserId == userId,
                    cancellationToken);
        }

        if (session is null)
        {
            var now = DateTime.UtcNow;
            session = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
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
}
