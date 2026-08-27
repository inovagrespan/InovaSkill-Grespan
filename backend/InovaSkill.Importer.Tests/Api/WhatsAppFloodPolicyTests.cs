using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.WhatsApp;

namespace InovaSkill.Importer.Tests.Api;

public sealed class WhatsAppFloodPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_AllowsMessagesBelowWindowLimit()
    {
        var decision = WhatsAppFloodPolicy.Evaluate(Now, null, 7, 8, TimeSpan.FromSeconds(30));

        Assert.True(decision.Allowed);
        Assert.False(decision.ShouldNotify);
        Assert.Null(decision.BlockedUntil);
    }

    [Fact]
    public void Evaluate_BlocksAndNotifiesOnceWhenWindowLimitIsReached()
    {
        var decision = WhatsAppFloodPolicy.Evaluate(Now, null, 8, 8, TimeSpan.FromSeconds(30));

        Assert.False(decision.Allowed);
        Assert.True(decision.ShouldNotify);
        Assert.Equal(Now.AddSeconds(30), decision.BlockedUntil);
    }

    [Fact]
    public void Evaluate_DoesNotRepeatNoticeDuringCooldown()
    {
        var decision = WhatsAppFloodPolicy.Evaluate(Now, Now.AddSeconds(1), 0, 8, TimeSpan.FromSeconds(30));

        Assert.False(decision.Allowed);
        Assert.False(decision.ShouldNotify);
    }

    [Fact]
    public void AggregationBatch_PreservesOrderAndQuestionLength()
    {
        var first = Receipt("Quais são", Now);
        var second = Receipt("as rotas críticas?", Now.AddMilliseconds(500));
        var tooLong = Receipt(new string('x', 800), Now.AddMilliseconds(700));

        var selected = WhatsAppMessageProcessor.SelectAggregationBatch([first, second, tooLong], 800);

        Assert.Equal([first, second], selected);
        Assert.Equal("Quais são as rotas críticas?", string.Join(" ", selected.Select(x => x.TextContent)));
    }

    private static WhatsAppMessageReceipt Receipt(string text, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(), ProviderMessageId = Guid.NewGuid().ToString(), TextContent = text,
        MessageType = "text", Direction = WhatsAppMessageDirections.Inbound,
        Status = WhatsAppMessageStatuses.Received, CreatedAt = createdAt, UpdatedAt = createdAt
    };
}
