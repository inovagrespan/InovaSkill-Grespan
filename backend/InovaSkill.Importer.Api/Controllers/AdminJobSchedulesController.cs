using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Cronos;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/job-schedules")]
public sealed class AdminJobSchedulesController(
    ImportDbContext db,
    IJobScheduleDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken) => Ok(
        await db.JobSchedules.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult> Create(JobScheduleRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return BadRequest(new { message = validation });
        var now = DateTime.UtcNow;
        var userId = ReadUserId();
        var schedule = new JobSchedule
        {
            Id = Guid.NewGuid(), Name = request.Name.Trim(), JobType = request.JobType,
            ContractVersion = request.ContractVersion, ParametersJson = request.Parameters.GetRawText(),
            CronExpression = request.CronExpression.Trim(), TimeZoneId = request.TimeZoneId,
            IsActive = request.IsActive, CreatedByUserId = userId, UpdatedByUserId = userId,
            CreatedAt = now, UpdatedAt = now,
            NextExecutionAt = GetNext(request.CronExpression, request.TimeZoneId)
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
        if (schedule.IsActive) dispatcher.AddOrUpdate(schedule.Id, schedule.CronExpression, schedule.TimeZoneId);
        return CreatedAtAction(nameof(List), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, JobScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = await db.JobSchedules.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (schedule is null) return NotFound();
        var validation = Validate(request);
        if (validation is not null) return BadRequest(new { message = validation });
        schedule.Name = request.Name.Trim(); schedule.JobType = request.JobType;
        schedule.ContractVersion = request.ContractVersion; schedule.ParametersJson = request.Parameters.GetRawText();
        schedule.CronExpression = request.CronExpression.Trim(); schedule.TimeZoneId = request.TimeZoneId;
        schedule.IsActive = request.IsActive; schedule.UpdatedByUserId = ReadUserId();
        schedule.UpdatedAt = DateTime.UtcNow; schedule.NextExecutionAt = GetNext(request.CronExpression, request.TimeZoneId);
        await db.SaveChangesAsync(cancellationToken);
        if (schedule.IsActive) dispatcher.AddOrUpdate(id, schedule.CronExpression, schedule.TimeZoneId);
        else dispatcher.Remove(id);
        return Ok(schedule);
    }

    [HttpPost("{id:guid}/pause")]
    public Task<ActionResult> Pause(Guid id, CancellationToken cancellationToken) => SetActive(id, false, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    public Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken) => SetActive(id, true, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await db.JobSchedules.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (schedule is null) return NotFound();
        dispatcher.Remove(id); db.JobSchedules.Remove(schedule); await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult> SetActive(Guid id, bool active, CancellationToken cancellationToken)
    {
        var schedule = await db.JobSchedules.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (schedule is null) return NotFound();
        schedule.IsActive = active; schedule.UpdatedAt = DateTime.UtcNow; schedule.UpdatedByUserId = ReadUserId();
        schedule.NextExecutionAt = active ? GetNext(schedule.CronExpression, schedule.TimeZoneId) : null;
        await db.SaveChangesAsync(cancellationToken);
        if (active) dispatcher.AddOrUpdate(id, schedule.CronExpression, schedule.TimeZoneId); else dispatcher.Remove(id);
        return Ok(schedule);
    }

    private static string? Validate(JobScheduleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Nome é obrigatório.";
        if (!OperationalJobCatalog.TryGet(request.JobType, out var definition)) return "Tipo de job desconhecido.";
        if (!definition.ScheduleAllowed) return "Este job não permite agendamento.";
        if (request.ContractVersion != definition.ContractVersion) return "Versão de contrato não suportada.";
        if (Encoding.UTF8.GetByteCount(request.Parameters.GetRawText()) > GenericJobPolicy.MaximumJsonBytes) return "O JSON excede 1 MB.";
        try { _ = CronExpression.Parse(request.CronExpression); }
        catch (CronFormatException) { return "Expressão cron inválida."; }
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (TimeZoneNotFoundException) { return "Fuso horário inválido."; }
        return null;
    }

    private static DateTime? GetNext(string expression, string timeZoneId) =>
        CronExpression.Parse(expression).GetNextOccurrence(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId), inclusive: false);

    private long ReadUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : 0;
    }
}

public sealed record JobScheduleRequest(string Name, string JobType, int ContractVersion,
    JsonElement Parameters, string CronExpression, string TimeZoneId, bool IsActive = true);
