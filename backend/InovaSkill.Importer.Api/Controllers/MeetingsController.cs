using System.Security.Claims;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Services;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController(ImportDbContext dbContext, IMeetingAiAnalysisService aiAnalysisService) : ControllerBase
{
    private const string DirectorRole = "diretor";
    private static readonly string[] StageFlow =
    [
        MeetingStage.Context,
        MeetingStage.Discussion,
        MeetingStage.Problems,
        MeetingStage.QuestionsAndAnswers,
        MeetingStage.AiAnalysis,
        MeetingStage.Conclusion,
        MeetingStage.Actions,
        MeetingStage.FollowUp
    ];

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> GetUsers(CancellationToken ct = default)
    {
        var users = await dbContext.AppUsers
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UserListItemDto(u.Id, u.Name, u.Email, u.Role, ""))
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MeetingListDto>>> GetMeetings(CancellationToken ct = default)
    {
        var current = CurrentUser();
        var meetings = await dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.CreatedByUserId == current.Id || m.Participants.Any(p => p.UserId == current.Id))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MeetingListDto(
                m.Id, m.Title, m.Description, m.Status, m.CurrentStage, m.CreatedByName,
                m.CreatedAt, m.ScheduledAt,
                m.Participants.Count,
                m.Problems.Count,
                m.Questions.Count,
                m.Actions.Count(a => a.Status == ActionStatus.Overdue)))
            .ToListAsync(ct);

        return Ok(meetings);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<MeetingDetailDto>> GetMeeting(long id, CancellationToken ct = default)
    {
        var meeting = await dbContext.Meetings
            .AsNoTracking()
            .Include(m => m.Participants)
            .Include(m => m.Comments)
            .Include(m => m.Problems).ThenInclude(p => p.Questions).ThenInclude(q => q.Answer)
            .Include(m => m.Questions).ThenInclude(q => q.Answer)
            .Include(m => m.AiAnalyses)
            .Include(m => m.Decisions)
            .Include(m => m.Actions)
            .Include(m => m.History)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (meeting is null) return NotFound();
        if (!CanAccessMeeting(meeting, CurrentUser())) return Forbid();

        var pendencies = await dbContext.CriticalPendencies
            .AsNoTracking()
            .Where(p => p.SourceMeetingId == id || string.IsNullOrWhiteSpace(p.SourceMeetingId.ToString()))
            .OrderByDescending(p => p.Priority).ThenBy(p => p.DeadlineAt)
            .Take(20)
            .Select(p => new CriticalPendingSummaryDto(
                p.Id, p.Title, p.Description, p.Origin, p.Sector, p.ResponsibleName,
                p.Priority, p.Status, p.DeadlineDays, p.DeadlineAt, p.CreatedAt))
            .ToListAsync(ct);

        return Ok(ToDetailDto(meeting, pendencies));
    }

    [HttpPost]
    public async Task<ActionResult<MeetingDetailDto>> CreateMeeting([FromBody] CreateMeetingRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!string.Equals(current.Role, DirectorRole, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var now = DateTime.UtcNow;
        var users = await dbContext.AppUsers
            .Where(u => request.ParticipantUserIds.Contains(u.Id))
            .ToListAsync(ct);

        var meeting = new Meeting
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Reason = request.Reason.Trim(),
            Status = MeetingStatus.Draft,
            CurrentStage = MeetingStage.Context,
            CreatedByUserId = current.Id,
            CreatedByName = current.Name ?? current.Email ?? "",
            CreatedAt = now,
            ScheduledAt = request.ScheduledAt,
            Context = request.Context.Trim(),
            InvolvedAreasCsv = request.InvolvedAreasCsv ?? "",
            Participants = users.Select(u => new MeetingParticipant
            {
                UserId = u.Id,
                UserName = u.Name,
                UserEmail = u.Email,
                UserRole = u.Role,
                RoleInMeeting = string.Equals(u.Role, DirectorRole, StringComparison.OrdinalIgnoreCase)
                    ? MeetingParticipantRole.Director : MeetingParticipantRole.Participant,
                ParticipationStatus = ParticipationStatusValue.Invited,
                InvitedAt = now
            }).ToList(),
        };

        dbContext.Meetings.Add(meeting);
        AddHistory(meeting, MeetingHistoryEvent.Created, "Reunião criada em rascunho.", current);
        await dbContext.SaveChangesAsync(ct);

        foreach (var participant in meeting.Participants)
        {
            dbContext.Notifications.Add(new Notification
            {
                UserId = participant.UserId,
                Title = "Convite para reunião",
                Message = $"Você foi convidado para a reunião: {meeting.Title}.",
                Type = NotificationType.MeetingInvite,
                Priority = NotificationPriority.High,
                RelatedLink = $"/reunioes/{meeting.Id}",
                RelatedEntity = "Meeting",
                RelatedEntityId = meeting.Id,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(ct);

        var pendencies = await GetRelatedPendencies(meeting, ct);
        return Ok(ToDetailDto(meeting, pendencies));
    }

    [HttpPost("{id:long}/start")]
    public async Task<ActionResult<MeetingDetailDto>> StartMeeting(long id, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current)) return Forbid();

        var meeting = await LoadMeetingAsync(id, ct);
        if (meeting is null) return NotFound();

        var validation = ValidateStageChange(meeting, MeetingStage.Discussion, false, "Iniciar reunião");
        if (validation is not null) return BadRequest(validation);

        var before = meeting.Status;
        meeting.Status = MeetingStatus.InProgress;
        meeting.CurrentStage = MeetingStage.Context;
        AddHistory(meeting, MeetingHistoryEvent.Started, "Reunião iniciada pelo Diretor.", current, before, meeting.Status);
        await dbContext.SaveChangesAsync(ct);

        var pendencies = await GetRelatedPendencies(meeting, ct);
        return Ok(ToDetailDto(meeting, pendencies));
    }

    [HttpPut("{id:long}/stage")]
    public async Task<ActionResult<MeetingDetailDto>> UpdateStage(long id, [FromBody] UpdateMeetingStageRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current))
            return Forbid();

        var meeting = await LoadMeetingAsync(id, ct);
        if (meeting is null) return NotFound();

        var previousStage = meeting.CurrentStage;
        var normalizedStage = NormalizeStage(request.Stage);
        var validation = ValidateStageChange(meeting, normalizedStage, request.Force, request.Justification);
        if (validation is not null) return BadRequest(validation);

        meeting.CurrentStage = normalizedStage;
        meeting.Status = StatusForStage(normalizedStage, meeting.Status);
        AddHistory(meeting, MeetingHistoryEvent.StageChanged, $"Etapa alterada de {TranslateStage(previousStage)} para {TranslateStage(normalizedStage)}.", current, previousStage, normalizedStage);
        await dbContext.SaveChangesAsync(ct);

        foreach (var participant in meeting.Participants.Where(p => p.UserId != current.Id))
        {
            dbContext.Notifications.Add(new Notification
            {
                UserId = participant.UserId,
                Title = "Mudança de etapa",
                Message = $"A reunião {meeting.Title} avançou para a etapa {TranslateStage(normalizedStage)}.",
                Type = NotificationType.MeetingStageChange,
                Priority = NotificationPriority.Medium,
                RelatedLink = $"/reunioes/{meeting.Id}",
                RelatedEntity = "Meeting",
                RelatedEntityId = meeting.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(ct);
        var pendencies = await GetRelatedPendencies(meeting, ct);
        return Ok(ToDetailDto(meeting, pendencies));
    }

    [HttpPost("{id:long}/comments")]
    public async Task<ActionResult<MeetingCommentDto>> AddComment(long id, [FromBody] AddCommentRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        var meeting = await dbContext.Meetings.Include(m => m.Participants).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();
        if (!CanAccessMeeting(meeting, current)) return Forbid();

        var comment = new MeetingComment
        {
            MeetingId = id,
            UserId = current.Id,
            UserName = current.Name ?? current.Email ?? "",
            Message = request.Message.Trim(),
            Stage = request.Stage,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.MeetingComments.Add(comment);
        AddHistory(meeting, MeetingHistoryEvent.CommentAdded, $"Comentário adicionado na etapa {TranslateStage(request.Stage)}.", current);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new MeetingCommentDto(comment.Id, comment.UserId, comment.UserName, comment.Message, comment.Stage, comment.IsImportant, comment.CreatedAt));
    }

    [HttpPost("{id:long}/problems")]
    public async Task<ActionResult<MeetingProblemDto>> AddProblem(long id, [FromBody] AddProblemRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current)) return Forbid();
        var meeting = await dbContext.Meetings.Include(m => m.Problems).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();

        var problem = new MeetingProblem
        {
            MeetingId = id,
            Sector = request.Sector.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Origin = request.Origin,
            CreatedByUserId = current.Id,
            CreatedByName = current.Name ?? current.Email ?? "",
            CreatedAt = DateTime.UtcNow
        };
        meeting.Problems.Add(problem);
        AddHistory(meeting, MeetingHistoryEvent.ProblemCreated, $"Problema criado para o setor {problem.Sector}.", current);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new MeetingProblemDto(problem.Id, problem.Sector, problem.Description, problem.Severity, problem.Origin,
            problem.CreatedByUserId, problem.CreatedByName, problem.ApprovedByDirector, problem.AiSuggestion,
            problem.CreatedAt, []));
    }

    [HttpPost("{id:long}/problems/suggest")]
    public async Task<ActionResult<IReadOnlyList<MeetingProblemDto>>> GenerateSuggestedProblems(long id, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current)) return Forbid();

        var meeting = await dbContext.Meetings.Include(m => m.Comments).Include(m => m.Problems).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();

        var suggestions = new[]
        {
            new MeetingProblem { MeetingId = id, Sector = "Produção", Description = "Produção está abaixo do esperado e pode comprometer entregas futuras.", Severity = ProblemSeverity.High, Origin = ProblemOrigin.AiSuggestion, CreatedByUserId = current.Id, CreatedByName = current.Name ?? current.Email ?? "", ApprovedByDirector = false, AiSuggestion = "Validar matéria-prima, equipe e máquinas." },
            new MeetingProblem { MeetingId = id, Sector = "Logística", Description = "Risco de entrega parcial se o volume produzido não for confirmado.", Severity = ProblemSeverity.Medium, Origin = ProblemOrigin.AiSuggestion, CreatedByUserId = current.Id, CreatedByName = current.Name ?? current.Email ?? "", ApprovedByDirector = false, AiSuggestion = "Confirmar capacidade de rota e janela de entrega." }
        };

        foreach (var suggestion in suggestions.Where(s => !meeting.Problems.Any(p => p.Description == s.Description)))
        {
            meeting.Problems.Add(suggestion);
        }

        AddHistory(meeting, MeetingHistoryEvent.ProblemCreated, "Problemas sugeridos pela IA mockada a partir da discussão.", current);
        await dbContext.SaveChangesAsync(ct);

        return Ok(meeting.Problems.Select(ToProblemDto).ToList());
    }

    [HttpPost("problems/{problemId:long}/approve")]
    public async Task<ActionResult<MeetingProblemDto>> ApproveProblem(long problemId, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current)) return Forbid();

        var problem = await dbContext.MeetingProblems.Include(p => p.Questions).ThenInclude(q => q.Answer).Include(p => p.Meeting).FirstOrDefaultAsync(p => p.Id == problemId, ct);
        if (problem is null) return NotFound();

        problem.ApprovedByDirector = true;
        AddHistory(problem.Meeting, MeetingHistoryEvent.ProblemApproved, $"Problema aprovado: {problem.Description}.", current);
        await dbContext.SaveChangesAsync(ct);

        return Ok(ToProblemDto(problem));
    }

    [HttpPost("{id:long}/questions")]
    public async Task<ActionResult<MeetingQuestionDto>> AddQuestion(long id, [FromBody] AddQuestionRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current))
            return Forbid();

        var meeting = await dbContext.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();

        var responsible = await dbContext.AppUsers.FindAsync([request.ResponsibleUserId], ct);
        var question = new MeetingQuestion
        {
            MeetingId = id,
            ProblemId = request.ProblemId,
            Question = request.Question.Trim(),
            ResponsibleUserId = request.ResponsibleUserId,
            ResponsibleName = responsible?.Name ?? "",
            Sector = request.Sector,
            IsRequired = request.IsRequired,
            Status = QuestionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.MeetingQuestions.Add(question);
        AddHistory(meeting, MeetingHistoryEvent.QuestionCreated, $"Pergunta criada para {question.ResponsibleName}.", current);
        await dbContext.SaveChangesAsync(ct);

        dbContext.Notifications.Add(new Notification
        {
            UserId = request.ResponsibleUserId,
            Title = "Pergunta pendente",
            Message = $"Você precisa responder uma pergunta sobre {request.Sector} na reunião {meeting.Title}.",
            Type = NotificationType.QuestionPending,
            Priority = NotificationPriority.High,
            RelatedLink = $"/reunioes/{meeting.Id}",
            RelatedEntity = "MeetingQuestion",
            RelatedEntityId = question.Id,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(ct);

        return Ok(new MeetingQuestionDto(question.Id, question.ProblemId, question.Question,
            question.ResponsibleUserId, question.ResponsibleName, question.Sector, question.IsRequired,
            question.Status, question.AnswerDeadline, question.CreatedAt, null));
    }

    [HttpPost("questions/{questionId:long}/answer")]
    public async Task<ActionResult<MeetingAnswerDto>> AnswerQuestion(long questionId, [FromBody] AnswerQuestionRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        var question = await dbContext.MeetingQuestions.Include(q => q.Answer).Include(q => q.Meeting).ThenInclude(m => m.Participants).FirstOrDefaultAsync(q => q.Id == questionId, ct);
        if (question is null) return NotFound();
        if (question.ResponsibleUserId != current.Id) return Forbid();
        if (question.Meeting.Status == MeetingStatus.Concluded) return BadRequest("Reunião concluída não aceita alteração de resposta.");

        var existingAnswer = question.Answer;
        if (existingAnswer is not null)
        {
            existingAnswer.Answer = request.Answer.Trim();
            existingAnswer.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var answer = new MeetingAnswer
            {
                QuestionId = questionId,
                UserId = current.Id,
                UserName = current.Name ?? current.Email ?? "",
                Answer = request.Answer.Trim(),
                Sector = question.Sector,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.MeetingAnswers.Add(answer);
        }

        question.Status = QuestionStatus.Answered;
        AddHistory(question.Meeting, MeetingHistoryEvent.AnswerSubmitted, $"Resposta registrada para a pergunta {question.Id}.", current);
        await dbContext.SaveChangesAsync(ct);

        var savedAnswer = question.Answer ?? await dbContext.MeetingAnswers.FirstAsync(a => a.QuestionId == questionId, ct);
        return Ok(new MeetingAnswerDto(savedAnswer.Id, savedAnswer.UserId, savedAnswer.UserName, savedAnswer.Sector, savedAnswer.Answer, savedAnswer.CreatedAt));
    }

    [HttpPost("{id:long}/ai-analysis/generate")]
    public async Task<ActionResult<IReadOnlyList<MeetingAiAnalysisDto>>> GenerateAiAnalysis(long id, [FromBody] GenerateMeetingAiAnalysisRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current)) return Forbid();

        var meeting = await LoadMeetingAsync(id, ct);
        if (meeting is null) return NotFound();
        var validation = ValidateAiAnalysisRequest(meeting, request.Force, request.Justification);
        if (validation is not null) return BadRequest(validation);

        var analyses = await aiAnalysisService.GenerateAnalysisAsync(id, ct);
        meeting = await LoadMeetingAsync(id, ct) ?? meeting;
        meeting.Status = MeetingStatus.InAiAnalysis;
        AddHistory(meeting, MeetingHistoryEvent.AiAnalysisGenerated, "Análise da IA gerada para problemas aprovados.", current);
        await dbContext.SaveChangesAsync(ct);

        return Ok(analyses.Select(ToAiAnalysisDto).ToList());
    }

    [HttpPost("{id:long}/decisions")]
    public async Task<ActionResult<MeetingDecisionDto>> CreateDecision(long id, [FromBody] CreateDecisionRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current))
            return Forbid();

        var meeting = await dbContext.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();

        var problem = await dbContext.MeetingProblems.FindAsync([request.ProblemId], ct);
        var responsible = await dbContext.AppUsers.FindAsync([request.ResponsibleUserId], ct);

        var decision = new MeetingDecision
        {
            MeetingId = id,
            ProblemId = request.ProblemId,
            ProblemDescription = problem?.Description ?? "",
            ChosenSolution = request.ChosenSolution.Trim(),
            SolutionOrigin = request.SolutionOrigin,
            Justification = request.Justification.Trim(),
            ResponsibleUserId = request.ResponsibleUserId,
            ResponsibleName = responsible?.Name ?? "",
            DeadlineDays = request.DeadlineDays,
            Priority = request.Priority,
            TrackingMetric = request.TrackingMetric ?? "",
            AcceptedRisk = request.AcceptedRisk ?? "",
            NextSteps = request.NextSteps ?? "",
            ClosedPendencies = request.ClosedPendencies ?? "",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.MeetingDecisions.Add(decision);
        AddHistory(meeting, MeetingHistoryEvent.DecisionCreated, $"Decisão criada para o problema {request.ProblemId}.", current);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new MeetingDecisionDto(decision.Id, decision.ProblemId, decision.ProblemDescription,
            decision.ChosenSolution, decision.SolutionOrigin, decision.Justification, decision.ResponsibleUserId,
            decision.ResponsibleName, decision.Sector, decision.DeadlineDays, decision.Priority,
            decision.TrackingMetric, decision.AcceptedRisk, decision.NextSteps, decision.ClosedPendencies,
            decision.CreatedAt));
    }

    [HttpPost("{id:long}/actions")]
    public async Task<ActionResult<MeetingActionDto>> CreateAction(long id, [FromBody] CreateActionRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current))
            return Forbid();

        var meeting = await dbContext.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();

        var responsible = await dbContext.AppUsers.FindAsync([request.ResponsibleUserId], ct);
        var now = DateTime.UtcNow;
        var deadline = now.AddDays(request.DeadlineDays);

        var action = new MeetingAction
        {
            MeetingId = id,
            DecisionId = request.DecisionId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            ResponsibleUserId = request.ResponsibleUserId,
            ResponsibleName = responsible?.Name ?? "",
            Sector = request.Sector,
            DeadlineDays = request.DeadlineDays,
            Priority = request.Priority,
            Status = ActionStatus.Pending,
            CreatedAt = now,
            DeadlineAt = deadline
        };
        dbContext.MeetingActions.Add(action);
        AddHistory(meeting, MeetingHistoryEvent.ActionCreated, $"Ação criada: {action.Title}.", current);
        await dbContext.SaveChangesAsync(ct);

        dbContext.Notifications.Add(new Notification
        {
            UserId = request.ResponsibleUserId,
            Title = "Ação atribuída",
            Message = $"Uma ação foi atribuída a você: {action.Title}.",
            Type = NotificationType.ActionAssigned,
            Priority = NotificationPriority.High,
            RelatedLink = $"/reunioes/{meeting.Id}",
            RelatedEntity = "MeetingAction",
            RelatedEntityId = action.Id,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(ct);

        return Ok(new MeetingActionDto(action.Id, action.DecisionId, action.Title, action.Description,
            action.ResponsibleUserId, action.ResponsibleName, action.Sector, action.DeadlineDays,
            action.Priority, action.Status, action.CompletionEvidence, action.Comments,
            action.CreatedAt, action.CompletedAt, action.DeadlineAt));
    }

    [HttpPut("actions/{actionId:long}/status")]
    public async Task<ActionResult<MeetingActionDto>> UpdateActionStatus(long actionId, [FromBody] UpdateActionStatusRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        var action = await dbContext.MeetingActions.FirstOrDefaultAsync(a => a.Id == actionId, ct);
        if (action is null) return NotFound();
        if (action.ResponsibleUserId != current.Id && !string.Equals(current.Role, DirectorRole, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        action.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.CompletionEvidence))
            action.CompletionEvidence = request.CompletionEvidence.Trim();
        if (!string.IsNullOrWhiteSpace(request.Comments))
            action.Comments = request.Comments.Trim();
        if (request.Status == ActionStatus.Completed)
            action.CompletedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Ok(new MeetingActionDto(action.Id, action.DecisionId, action.Title, action.Description,
            action.ResponsibleUserId, action.ResponsibleName, action.Sector, action.DeadlineDays,
            action.Priority, action.Status, action.CompletionEvidence, action.Comments,
            action.CreatedAt, action.CompletedAt, action.DeadlineAt));
    }

    [HttpPost("{id:long}/conclude")]
    public async Task<ActionResult> ConcludeMeeting(long id, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current))
            return Forbid();

        var meeting = await dbContext.Meetings.Include(m => m.Participants).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();

        var now = DateTime.UtcNow;
        meeting.Status = MeetingStatus.Concluded;
        meeting.ConcludedAt = now;
        meeting.CurrentStage = MeetingStage.FollowUp;
        AddHistory(meeting, MeetingHistoryEvent.Completed, "Reunião encerrada pelo Diretor.", current);
        await dbContext.SaveChangesAsync(ct);

        foreach (var participant in meeting.Participants.Where(p => p.UserId != current.Id))
        {
            dbContext.Notifications.Add(new Notification
            {
                UserId = participant.UserId,
                Title = "Reunião concluída",
                Message = $"A reunião {meeting.Title} foi concluída.",
                Type = NotificationType.MeetingConcluded,
                Priority = NotificationPriority.Medium,
                RelatedLink = $"/reunioes/{meeting.Id}",
                RelatedEntity = "Meeting",
                RelatedEntityId = meeting.Id,
                CreatedAt = now
            });
        }

        return Ok();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult> CancelMeeting(long id, [FromBody] CancelMeetingRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current)) return Forbid();

        var meeting = await dbContext.Meetings.Include(m => m.Participants).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();

        meeting.Status = MeetingStatus.Cancelled;
        meeting.CancellationReason = request.Reason.Trim();
        AddHistory(meeting, MeetingHistoryEvent.Cancelled, $"Reunião cancelada: {meeting.CancellationReason}", current);
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpGet("{id:long}/briefing")]
    public async Task<ActionResult<PreMeetingBriefingDto>> GetPreMeetingBriefing(long id, CancellationToken ct = default)
    {
        var current = CurrentUser();
        var meeting = await dbContext.Meetings.Include(m => m.Participants).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return NotFound();
        if (!CanAccessMeeting(meeting, current)) return Forbid();

        var pendencies = await dbContext.CriticalPendencies
            .AsNoTracking()
            .Where(p => p.Status != PendingStatus.Resolved && p.Status != PendingStatus.CancelledWithJustification)
            .Where(p => string.IsNullOrWhiteSpace(meeting.InvolvedAreasCsv) || p.Sector == "" || meeting.InvolvedAreasCsv.Contains(p.Sector))
            .OrderByDescending(p => p.Priority).ThenBy(p => p.DeadlineAt)
            .Take(20)
            .Select(p => new CriticalPendingSummaryDto(
                p.Id, p.Title, p.Description, p.Origin, p.Sector, p.ResponsibleName,
                p.Priority, p.Status, p.DeadlineDays, p.DeadlineAt, p.CreatedAt))
            .ToListAsync(ct);

        var aiSummary = pendencies.Count > 0
            ? $"Antes de iniciar esta reunião, existem {pendencies.Count} pendências críticas relacionadas: " +
              string.Join("; ", pendencies.Take(3).Select(p => $"{p.Title} ({p.Status})")) + "."
            : "Nenhuma pendência crítica identificada para esta reunião.";

        return Ok(new PreMeetingBriefingDto(pendencies.Count, pendencies, aiSummary));
    }

    [HttpPost("{id:long}/add-pending")]
    public async Task<ActionResult> AddPendingToMeeting(long id, [FromBody] AddPendingToMeetingRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        if (!IsDirector(current)) return Forbid();

        var pending = await dbContext.CriticalPendencies.FindAsync([request.PendingId], ct);
        if (pending is null) return NotFound();
        pending.SourceMeetingId = id;
        var meeting = await dbContext.Meetings.FindAsync([id], ct);
        if (meeting is not null)
        {
            AddHistory(meeting, MeetingHistoryEvent.PendingAdded, $"Pendência adicionada à reunião: {pending.Title}.", current);
        }
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    private async Task<Meeting?> LoadMeetingAsync(long id, CancellationToken ct)
    {
        return await dbContext.Meetings
            .Include(m => m.Participants)
            .Include(m => m.Comments)
            .Include(m => m.Problems).ThenInclude(p => p.Questions).ThenInclude(q => q.Answer)
            .Include(m => m.Questions).ThenInclude(q => q.Answer)
            .Include(m => m.AiAnalyses)
            .Include(m => m.Decisions)
            .Include(m => m.Actions)
            .Include(m => m.History)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    private async Task<IReadOnlyList<CriticalPendingSummaryDto>> GetRelatedPendencies(Meeting meeting, CancellationToken ct)
    {
        return await dbContext.CriticalPendencies
            .AsNoTracking()
            .Where(p => p.SourceMeetingId == meeting.Id || p.Status != PendingStatus.Resolved)
            .OrderByDescending(p => p.Priority).ThenBy(p => p.DeadlineAt)
            .Take(10)
            .Select(p => new CriticalPendingSummaryDto(
                p.Id, p.Title, p.Description, p.Origin, p.Sector, p.ResponsibleName,
                p.Priority, p.Status, p.DeadlineDays, p.DeadlineAt, p.CreatedAt))
            .ToListAsync(ct);
    }

    private static string TranslateStage(string stage) => stage switch
    {
        "contexto" => "Contexto",
        "discussao" => "Discussão",
        "problemas" => "Problemas por setor",
        "perguntas_e_respostas" => "Perguntas e respostas",
        "solucoes" => "Perguntas e respostas",
        "analise_ia" => "Análise da IA",
        "conclusao" => "Conclusão",
        "acoes" => "Ações",
        "acompanhamento" => "Acompanhamento futuro",
        _ => stage
    };

    private static MeetingDetailDto ToDetailDto(Meeting m, IReadOnlyList<CriticalPendingSummaryDto> pendencies) => new(
        m.Id, m.Title, m.Description, m.Reason, m.Status, m.CurrentStage,
        m.CreatedByUserId, m.CreatedByName, m.CreatedAt, m.ScheduledAt, m.ConcludedAt,
        m.Context, m.InvolvedAreasCsv, m.AiSummary, m.CancellationReason,
        m.Participants.Select(p => new MeetingParticipantDto(p.Id, p.UserId, p.UserName, p.UserEmail,
            p.UserRole, p.UserSector, p.RoleInMeeting, p.ParticipationStatus, p.InvitedAt)).ToList(),
        m.Comments.Select(c => new MeetingCommentDto(c.Id, c.UserId, c.UserName, c.Message, c.Stage, c.IsImportant, c.CreatedAt)).ToList(),
        m.Problems.Select(ToProblemDto).ToList(),
        m.Questions.Select(ToQuestionDto).ToList(),
        m.AiAnalyses.Select(ToAiAnalysisDto).ToList(),
        m.Decisions.Select(d => new MeetingDecisionDto(d.Id, d.ProblemId, d.ProblemDescription, d.ChosenSolution,
            d.SolutionOrigin, d.Justification, d.ResponsibleUserId, d.ResponsibleName, d.Sector,
            d.DeadlineDays, d.Priority, d.TrackingMetric, d.AcceptedRisk, d.NextSteps, d.ClosedPendencies, d.CreatedAt)).ToList(),
        m.Actions.Select(a => new MeetingActionDto(a.Id, a.DecisionId, a.Title, a.Description,
            a.ResponsibleUserId, a.ResponsibleName, a.Sector, a.DeadlineDays, a.Priority,
            a.Status, a.CompletionEvidence, a.Comments, a.CreatedAt, a.CompletedAt, a.DeadlineAt)).ToList(),
        m.History.OrderBy(h => h.CreatedAt).Select(h => new MeetingHistoryDto(h.Id, h.EventType, h.Description, h.UserId, h.UserName, h.DataBefore, h.DataAfter, h.CreatedAt)).ToList(),
        pendencies);

    private static MeetingProblemDto ToProblemDto(MeetingProblem p) => new(
        p.Id, p.Sector, p.Description, p.Severity, p.Origin,
        p.CreatedByUserId, p.CreatedByName, p.ApprovedByDirector, p.AiSuggestion, p.CreatedAt,
        p.Questions.Select(ToQuestionDto).ToList());

    private static MeetingAiAnalysisDto ToAiAnalysisDto(MeetingAiAnalysis a) => new(
        a.Id, a.ProblemId, a.ProblemDescription, a.ProposedSolution, a.MakesSense,
        a.PositivePoints, a.NegativePoints, a.Risks, a.ExpectedImpact, a.Recommendation,
        a.AlternativeSolution, a.SuggestedDecision, a.RelatedPendencies, a.CreatedAt);

    private static MeetingQuestionDto ToQuestionDto(MeetingQuestion q) => new(
        q.Id, q.ProblemId, q.Question, q.ResponsibleUserId, q.ResponsibleName, q.Sector,
        q.IsRequired, q.Status, q.AnswerDeadline, q.CreatedAt,
            q.Answer is not null ? new MeetingAnswerDto(q.Answer.Id, q.Answer.UserId, q.Answer.UserName,
            q.Answer.Sector, q.Answer.Answer, q.Answer.CreatedAt) : null);

    private static bool IsDirector((long Id, string? Name, string? Email, string Role) user) =>
        string.Equals(user.Role, DirectorRole, StringComparison.OrdinalIgnoreCase);

    private static bool CanAccessMeeting(Meeting meeting, (long Id, string? Name, string? Email, string Role) user) =>
        IsDirector(user) || meeting.Participants.Any(p => p.UserId == user.Id);

    private static string NormalizeStage(string stage) =>
        string.Equals(stage, "solucoes", StringComparison.OrdinalIgnoreCase)
            ? MeetingStage.QuestionsAndAnswers
            : stage;

    private static string StatusForStage(string stage, string currentStatus) => stage switch
    {
        MeetingStage.QuestionsAndAnswers => MeetingStatus.AwaitingAnswers,
        MeetingStage.AiAnalysis => MeetingStatus.InAiAnalysis,
        MeetingStage.Conclusion => MeetingStatus.AwaitingConclusion,
        MeetingStage.FollowUp => currentStatus == MeetingStatus.Concluded ? MeetingStatus.Concluded : MeetingStatus.AwaitingConclusion,
        _ => currentStatus == MeetingStatus.Draft ? MeetingStatus.InProgress : currentStatus
    };

    private static string? ValidateStageChange(Meeting meeting, string nextStage, bool force, string justification)
    {
        if (!StageFlow.Contains(nextStage)) return "Etapa inválida.";

        var currentIndex = Array.IndexOf(StageFlow, meeting.CurrentStage);
        var targetIndex = Array.IndexOf(StageFlow, nextStage);
        if (currentIndex >= 0 && Math.Abs(targetIndex - currentIndex) > 1)
        {
            return "A reunião só pode avançar ou voltar uma etapa por vez.";
        }

        if (nextStage == MeetingStage.Discussion)
        {
            if (string.IsNullOrWhiteSpace(meeting.Title) || string.IsNullOrWhiteSpace(meeting.Reason) || meeting.Participants.Count == 0)
                return "Informe título, motivo e participantes antes de ir para discussão.";
        }

        if (nextStage == MeetingStage.Problems && meeting.Comments.Count == 0 && !force)
            return "Não há comentários. Confirme avanço forçado com justificativa.";

        if (nextStage == MeetingStage.QuestionsAndAnswers && !meeting.Problems.Any(p => p.ApprovedByDirector))
            return "Aprove pelo menos um problema antes de ir para perguntas e respostas.";

        if (nextStage == MeetingStage.AiAnalysis && meeting.Questions.Any(q => q.IsRequired && q.Status != QuestionStatus.Answered) && (!force || string.IsNullOrWhiteSpace(justification)))
            return "Existem perguntas obrigatórias sem resposta. Informe justificativa para avançar mesmo assim.";

        if (nextStage == MeetingStage.Conclusion && meeting.AiAnalyses.Count == 0 && (!force || string.IsNullOrWhiteSpace(justification)))
            return "Gere análise da IA ou informe justificativa para seguir sem análise.";

        if (nextStage == MeetingStage.Actions && meeting.Decisions.Count == 0)
            return "Crie pelo menos uma decisão antes de ir para ações.";

        if (nextStage == MeetingStage.FollowUp && meeting.Actions.Count == 0 && (!force || string.IsNullOrWhiteSpace(justification)))
            return "Crie ao menos uma ação ou informe justificativa para acompanhar sem ações.";

        return null;
    }

    private static string? ValidateAiAnalysisRequest(Meeting meeting, bool force, string justification)
    {
        if (!meeting.Problems.Any(p => p.ApprovedByDirector))
            return "A análise da IA exige pelo menos um problema aprovado.";

        if (meeting.Questions.Any(q => q.IsRequired && q.Status != QuestionStatus.Answered) && (!force || string.IsNullOrWhiteSpace(justification)))
            return "Existem perguntas obrigatórias sem resposta. Informe justificativa para gerar análise mesmo assim.";

        return null;
    }

    private void AddHistory(
        Meeting meeting,
        string eventType,
        string description,
        (long Id, string? Name, string? Email, string Role) user,
        string dataBefore = "{}",
        string dataAfter = "{}")
    {
        meeting.History.Add(new MeetingHistory
        {
            EventType = eventType,
            Description = description,
            UserId = user.Id,
            UserName = user.Name ?? user.Email ?? "",
            DataBefore = ToJsonLiteral(dataBefore),
            DataAfter = ToJsonLiteral(dataAfter),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string ToJsonLiteral(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "{}";
        return value.TrimStart().StartsWith('{') || value.TrimStart().StartsWith('[')
            ? value
            : $"{{\"value\":\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}}";
    }

    private (long Id, string? Name, string? Email, string Role) CurrentUser()
    {
        var claims = User;
        var idClaim = claims.FindFirstValue("sub") ?? claims.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(idClaim, out var userId);
        return (userId,
            claims.FindFirstValue("name") ?? claims.FindFirstValue(ClaimTypes.Name),
            claims.FindFirstValue("email") ?? claims.FindFirstValue(ClaimTypes.Email),
            claims.FindFirstValue("role") ?? claims.FindFirstValue(ClaimTypes.Role) ?? "");
    }
}
