using System.Text.Json;

namespace InovaSkill.Importer.Api.Assistant;

public enum AssistantScopeDecision { InScope, OutOfScope, Ambiguous }

public sealed class AssistantScopeClassifier(IChatModelClient modelClient, ILogger<AssistantScopeClassifier> logger)
{
    private static readonly object ClassificationFormat = new
    {
        type = "json_schema",
        name = "assistant_scope",
        strict = true,
        schema = new
        {
            type = "object",
            properties = new
            {
                decision = new { type = "string", @enum = new[] { "IN_SCOPE", "OUT_OF_SCOPE", "AMBIGUOUS" } }
            },
            required = new[] { "decision" },
            additionalProperties = false
        }
    };

    public async Task<AssistantScopeDecision> ClassifyAsync(
        string model,
        IReadOnlyList<ChatModelInputMessage> messages,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await modelClient.SendAsync(new ChatModelRequest(
                model,
                AssistantPrompts.ScopeClassificationPrompt,
                messages,
                [],
                Purpose: ChatModelRequestPurpose.ScopeClassification,
                TextFormat: ClassificationFormat), cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Text)) return AssistantScopeDecision.Ambiguous;
            using var document = JsonDocument.Parse(response.Text);
            return document.RootElement.GetProperty("decision").GetString() switch
            {
                "IN_SCOPE" => AssistantScopeDecision.InScope,
                "OUT_OF_SCOPE" => AssistantScopeDecision.OutOfScope,
                _ => AssistantScopeDecision.Ambiguous
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Classificação de escopo do assistente falhou de forma controlada.");
            return AssistantScopeDecision.Ambiguous;
        }
    }
}
