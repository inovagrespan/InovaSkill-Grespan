using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AssistantControllerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ask_RejectsEmptyQuestion(string question)
    {
        var controller = CreateController(maximumLength: 800);

        var result = await controller.Ask(new AssistantQuestionRequest(null, question, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Ask_RejectsQuestionAboveConfiguredLimit()
    {
        var controller = CreateController(maximumLength: 10);

        var result = await controller.Ask(
            new AssistantQuestionRequest(null, "pergunta muito longa", null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("10", Assert.IsType<ProblemDetails>(badRequest.Value).Detail);
    }

    [Fact]
    public async Task ListSessions_ReturnsOnlyTheAuthenticatedUsersConversationSummaries()
    {
        var sessionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var historyStore = new FakeHistoryStore(
            [new ChatSessionSummary(sessionId, "Como está a rota A?", DateTime.UnixEpoch)]);
        var controller = CreateController(800, historyStore);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "42")], "test"))
            }
        };

        var result = await controller.ListSessions(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var sessions = Assert.IsAssignableFrom<IEnumerable<AssistantConversationSummaryResponse>>(response.Value);
        var session = Assert.Single(sessions);
        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal(42, historyStore.LastUserId);
    }

    private static AssistantController CreateController(int maximumLength, IChatHistoryStore? historyStore = null)
    {
        // O serviço não é invocado nos cenários de validação.
        return new AssistantController(
            null!,
            historyStore ?? null!,
            Options.Create(new AssistantOptions { MaximumQuestionLength = maximumLength }));
    }

    private sealed class FakeHistoryStore(IReadOnlyList<ChatSessionSummary> sessions) : IChatHistoryStore
    {
        public long? LastUserId { get; private set; }

        public Task<ChatSessionSnapshot> LoadOrCreateAsync(Guid? sessionId, long userId, int maximumMessages, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AppendAsync(Guid sessionId, string role, string content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ChatSessionSummary>> ListAsync(long userId, int maximumSessions, CancellationToken cancellationToken)
        {
            LastUserId = userId;
            return Task.FromResult(sessions);
        }

        public Task<ChatSessionHistory?> LoadAsync(Guid sessionId, long userId, int maximumMessages, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
