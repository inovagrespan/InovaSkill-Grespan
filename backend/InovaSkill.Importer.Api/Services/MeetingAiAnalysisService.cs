using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Services;

public interface IMeetingAiAnalysisService
{
    Task<IReadOnlyList<MeetingAiAnalysis>> GenerateAnalysisAsync(long meetingId, CancellationToken ct = default);
}

public sealed class MeetingAiAnalysisService(ImportDbContext dbContext) : IMeetingAiAnalysisService
{
    public async Task<IReadOnlyList<MeetingAiAnalysis>> GenerateAnalysisAsync(long meetingId, CancellationToken ct = default)
    {
        var meeting = await dbContext.Meetings
            .Include(m => m.Problems).ThenInclude(p => p.Questions).ThenInclude(q => q.Answer)
            .Include(m => m.AiAnalyses)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct);

        if (meeting is null)
        {
            return [];
        }

        dbContext.MeetingAiAnalyses.RemoveRange(meeting.AiAnalyses);

        var analyses = meeting.Problems
            .Where(problem => problem.ApprovedByDirector)
            .Select(problem =>
            {
                var answers = problem.Questions
                    .Where(question => question.Answer is not null)
                    .Select(question => $"{question.Question}: {question.Answer!.Answer}")
                    .ToList();
                var consideredAnswers = answers.Count == 0
                    ? "Nenhuma resposta foi registrada para este problema."
                    : string.Join(" | ", answers);
                var proposedSolution = answers.Count == 0
                    ? "Ainda não existe solução do gestor validada por resposta."
                    : answers.First();

                return new MeetingAiAnalysis
                {
                    MeetingId = meetingId,
                    ProblemId = problem.Id,
                    ProblemDescription = problem.Description,
                    ProposedSolution = proposedSolution,
                    MakesSense = answers.Count > 0,
                    PositivePoints = answers.Count > 0
                        ? "A análise considera respostas específicas e separadas por pergunta, reduzindo decisão baseada apenas em percepção."
                        : "O problema está estruturado por setor e pode receber perguntas antes da conclusão.",
                    NegativePoints = answers.Count > 0
                        ? "A solução ainda precisa ser validada contra prazo, recursos e impacto em outras áreas."
                        : "A ausência de respostas limita a confiança da análise.",
                    Risks = "Se a causa principal não for confirmada, a decisão pode tratar sintomas e manter o problema recorrente.",
                    ExpectedImpact = $"Impacto esperado no setor {problem.Sector}: redução do risco operacional e melhor rastreabilidade da decisão.",
                    Recommendation = answers.Count > 0
                        ? "Validar a solução com o responsável, definir prazo e transformar a decisão em ação acompanhável."
                        : "Solicitar respostas obrigatórias antes de aprovar uma decisão definitiva ou registrar justificativa para seguir sem resposta.",
                    AlternativeSolution = "Criar ação de diagnóstico curto, confirmar causa raiz e revisar a decisão em reunião de acompanhamento.",
                    SuggestedDecision = $"Definir responsável de {problem.Sector}, prazo e indicador para acompanhar: {problem.Description}.",
                    RelatedPendencies = consideredAnswers,
                    CreatedAt = DateTime.UtcNow
                };
            })
            .ToList();

        dbContext.MeetingAiAnalyses.AddRange(analyses);
        await dbContext.SaveChangesAsync(ct);

        return analyses;
    }
}
