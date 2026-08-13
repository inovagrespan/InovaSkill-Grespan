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
    public async Task SimulateWhatsApp_RejectsEmptyMessageWithoutCallingProvider()
    {
        var controller = CreateController(maximumLength: 800);
        var result = await controller.SimulateWhatsApp(
            new AssistantQuestionRequest(null, "   ", null), CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Digite uma mensagem.", Assert.IsType<ProblemDetails>(badRequest.Value).Detail);
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

        var result = await controller.ListSessions(CancellationToken.None, 0);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<AssistantConversationPageResponse>(response.Value);
        var session = Assert.Single(page.Items);
        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal(42, historyStore.LastUserId);
        Assert.Equal(0, historyStore.LastOffset);
        Assert.Equal(21, historyStore.LastMaximumSessions);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task ListSessions_ReturnsTwentyItemsAndSignalsPreviousConversations()
    {
        var sessions = Enumerable.Range(1, 21)
            .Select(index => new ChatSessionSummary(Guid.NewGuid(), $"Conversa {index}", DateTime.UnixEpoch.AddMinutes(index)))
            .ToList();
        var historyStore = new FakeHistoryStore(sessions);
        var controller = CreateController(800, historyStore);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "42")], "test"))
            }
        };

        var result = await controller.ListSessions(CancellationToken.None, 20);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<AssistantConversationPageResponse>(response.Value);
        Assert.Equal(20, page.Items.Count);
        Assert.True(page.HasMore);
        Assert.Equal(40, page.NextOffset);
        Assert.Equal(20, historyStore.LastOffset);
        Assert.Equal(21, historyStore.LastMaximumSessions);
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
        public int? LastOffset { get; private set; }
        public int? LastMaximumSessions { get; private set; }

        public Task<ChatSessionSnapshot> LoadOrCreateAsync(Guid? sessionId, long userId, int maximumMessages, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AppendAsync(Guid sessionId, string role, string content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ChatSessionSummary>> ListAsync(long userId, int offset, int maximumSessions, CancellationToken cancellationToken)
        {
            LastUserId = userId;
            LastOffset = offset;
            LastMaximumSessions = maximumSessions;
            return Task.FromResult(sessions);
        }

        public Task<ChatSessionHistory?> LoadAsync(Guid sessionId, long userId, int maximumMessages, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
