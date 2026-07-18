using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AssistantControllerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ask_RejectsEmptyQuestion(string question)
    {
        var controller = CreateController(maximumLength: 800);

        var result = await controller.Ask(new AssistantQuestionRequest(question), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Ask_RejectsQuestionAboveConfiguredLimit()
    {
        var controller = CreateController(maximumLength: 10);

        var result = await controller.Ask(
            new AssistantQuestionRequest("pergunta muito longa"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("10", Assert.IsType<ProblemDetails>(badRequest.Value).Detail);
    }

    private static AssistantController CreateController(int maximumLength)
    {
        // O serviço não é invocado nos cenários de validação.
        return new AssistantController(
            null!,
            Options.Create(new AssistantOptions { MaximumQuestionLength = maximumLength }));
    }
}
