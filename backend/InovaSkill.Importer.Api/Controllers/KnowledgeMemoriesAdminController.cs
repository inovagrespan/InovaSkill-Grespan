using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/knowledge-memories")]
public sealed class KnowledgeMemoriesAdminController(ImportDbContext dbContext, KnowledgeMemoryService memoryService) : ControllerBase
{
    private const int MaximumPageSize = 100;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KnowledgeMemoryResponse>>> List(
        string? search, string? scope, long? ownerUserId, bool includeInactive = false, int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeMemories.AsNoTracking();
        if (!includeInactive) query = query.Where(memory => memory.IsActive);
        if (!string.IsNullOrWhiteSpace(scope)) query = query.Where(memory => memory.Scope == scope);
        if (ownerUserId.HasValue) query = query.Where(memory => memory.OwnerUserId == ownerUserId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(memory => EF.Functions.ILike(memory.Subject, $"%{term}%") || EF.Functions.ILike(memory.Content, $"%{term}%"));
        }
        return Ok(await query.OrderByDescending(memory => memory.UpdatedAt).Take(Math.Clamp(take, 1, MaximumPageSize))
            .Select(memory => new KnowledgeMemoryResponse(memory.Id, memory.Scope, memory.OwnerUserId,
                memory.OwnerUser == null ? null : memory.OwnerUser.Name, memory.CreatedByUserId, memory.CreatedByUser.Name,
                memory.Subject, memory.Content, memory.IsActive, memory.CreatedAt, memory.UpdatedAt, memory.SupersedesMemoryId))
            .ToListAsync(cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateKnowledgeMemoryRequest request, CancellationToken cancellationToken)
    {
        var memory = await dbContext.KnowledgeMemories.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (memory is null) return NotFound();
        var subject = request.Subject?.Trim() ?? string.Empty;
        var content = request.Content?.Trim() ?? string.Empty;
        if (subject.Length is 0 or > 256 || content.Length is 0 or > 2000) return BadRequest(new ProblemDetails { Detail = "Assunto ou conteúdo inválido." });
        memory.Subject = subject; memory.Content = content; memory.IsActive = request.IsActive;
        memory.EmbeddingJson = await memoryService.CreateEmbeddingJsonAsync($"{subject}: {content}", cancellationToken);
        memory.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var memory = await dbContext.KnowledgeMemories.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (memory is null) return NotFound();
        memory.IsActive = false; memory.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record KnowledgeMemoryResponse(Guid Id, string Scope, long? OwnerUserId, string? OwnerUserName,
    long CreatedByUserId, string CreatedByUserName, string Subject, string Content, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt, Guid? SupersedesMemoryId);
public sealed record UpdateKnowledgeMemoryRequest(string? Subject, string? Content, bool IsActive);
