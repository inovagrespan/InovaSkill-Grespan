using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class WhatsAppIntegrationMigrationTests
{
    [Fact]
    public void Up_CreatesIdentityAndIdempotencyIndexes()
    {
        var sql = Assert.Single(new AddWhatsAppIntegration().UpOperations.OfType<SqlOperation>()).Sql;
        Assert.Contains("IX_whatsapp_user_links_UserId", sql);
        Assert.Contains("IX_whatsapp_user_links_NormalizedPhone", sql);
        Assert.Contains("IX_whatsapp_message_receipts_ProviderMessageId", sql);
        Assert.Contains("IX_chat_sessions_WhatsAppUserLinkId_Channel", sql);
        Assert.Contains("WHERE \"WhatsAppUserLinkId\" IS NOT NULL", sql);
    }
}
