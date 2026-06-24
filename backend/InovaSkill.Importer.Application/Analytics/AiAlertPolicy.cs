using System.Globalization;
using System.Text;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.Analytics;

public static class AiAlertPolicy
{
    public static readonly IReadOnlySet<string> ValidAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AiAlertAreas.Vendas,
        AiAlertAreas.Logistica,
        AiAlertAreas.Producao,
        AiAlertAreas.Administrativo,
        AiAlertAreas.Diretoria
    };

    public static readonly IReadOnlySet<string> ValidSeverities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AiAlertSeverities.Baixo,
        AiAlertSeverities.Medio,
        AiAlertSeverities.Alto,
        AiAlertSeverities.Critico
    };

    public static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AiAlertStatuses.Novo,
        AiAlertStatuses.Visualizado,
        AiAlertStatuses.EmAnalise,
        AiAlertStatuses.ReuniaoSugerida,
        AiAlertStatuses.ReuniaoAgendada,
        AiAlertStatuses.DecisaoPendente,
        AiAlertStatuses.AcaoEmExecucao,
        AiAlertStatuses.Atrasado,
        AiAlertStatuses.EscaladoParaDiretoria,
        AiAlertStatuses.Resolvido,
        AiAlertStatuses.CanceladoComJustificativa
    };

    public static bool IsCritical(AiAlert alert)
    {
        return string.Equals(alert.Severity, AiAlertSeverities.Critico, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLate(AiAlert alert, DateTime now)
    {
        if (alert.IsResolved)
        {
            return false;
        }

        return now > alert.ResponseDeadlineAt ||
            (alert.ActionDeadlineAt.HasValue && now > alert.ActionDeadlineAt.Value);
    }

    public static bool IsVisibleTo(AiAlert alert, string? role, string? userName, string? userEmail)
    {
        var normalizedRole = Normalize(role);
        if (normalizedRole is "admin" or "admin_system")
        {
            return true;
        }

        if (normalizedRole is "diretor")
        {
            return IsCritical(alert) ||
                IsLate(alert, DateTime.UtcNow) ||
                string.Equals(alert.Status, AiAlertStatuses.EscaladoParaDiretoria, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alert.ResponsibleArea, AiAlertAreas.Diretoria, StringComparison.OrdinalIgnoreCase);
        }

        var userTokens = Csv(alert.InvolvedUsersCsv)
            .Append(alert.ResponsibleManager)
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(userName) && userTokens.Contains(Normalize(userName)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(userEmail) && userTokens.Contains(Normalize(userEmail)))
        {
            return true;
        }

        var roleArea = AreaFromRole(normalizedRole);
        if (roleArea is null)
        {
            return false;
        }

        return string.Equals(Normalize(alert.ResponsibleArea), Normalize(roleArea), StringComparison.OrdinalIgnoreCase) ||
            Csv(alert.InvolvedAreasCsv).Any(area => string.Equals(Normalize(area), Normalize(roleArea), StringComparison.OrdinalIgnoreCase));
    }

    public static string? AreaFromRole(string? normalizedRole)
    {
        return normalizedRole switch
        {
            "vendas" => AiAlertAreas.Vendas,
            "logistica" => AiAlertAreas.Logistica,
            "producao" => AiAlertAreas.Producao,
            "administrativo" => AiAlertAreas.Administrativo,
            "diretor" => AiAlertAreas.Diretoria,
            _ => null
        };
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static IReadOnlyList<string> Csv(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }
}
