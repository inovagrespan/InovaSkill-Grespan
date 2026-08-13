using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608110001_AddWhatsAppIntegration")]
public sealed class AddWhatsAppIntegration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE chat_sessions ADD COLUMN IF NOT EXISTS "Channel" character varying(16) NOT NULL DEFAULT 'web';

        CREATE TABLE IF NOT EXISTS whatsapp_connections (
            "Id" integer NOT NULL,
            "InstanceName" character varying(128) NOT NULL,
            "Status" character varying(32) NOT NULL,
            "ConnectedPhone" character varying(20),
            "ConnectedAt" timestamptz,
            "LastEventAt" timestamptz,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_whatsapp_connections" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS whatsapp_user_links (
            "Id" uuid NOT NULL,
            "UserId" bigint NOT NULL,
            "NormalizedPhone" character varying(20) NOT NULL,
            "Status" character varying(16) NOT NULL,
            "VerificationCodeHash" character varying(128),
            "VerificationExpiresAt" timestamptz,
            "VerificationAttempts" integer NOT NULL,
            "ConfirmedAt" timestamptz,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_whatsapp_user_links" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_whatsapp_user_links_app_users_UserId" FOREIGN KEY ("UserId") REFERENCES app_users ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_whatsapp_user_links_UserId" ON whatsapp_user_links ("UserId");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_whatsapp_user_links_NormalizedPhone" ON whatsapp_user_links ("NormalizedPhone");

        ALTER TABLE chat_sessions ADD COLUMN IF NOT EXISTS "WhatsAppUserLinkId" uuid;
        ALTER TABLE chat_sessions DROP CONSTRAINT IF EXISTS "FK_chat_sessions_whatsapp_user_links_WhatsAppUserLinkId";
        ALTER TABLE chat_sessions ADD CONSTRAINT "FK_chat_sessions_whatsapp_user_links_WhatsAppUserLinkId"
            FOREIGN KEY ("WhatsAppUserLinkId") REFERENCES whatsapp_user_links ("Id") ON DELETE CASCADE;
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_chat_sessions_WhatsAppUserLinkId_Channel"
            ON chat_sessions ("WhatsAppUserLinkId", "Channel") WHERE "WhatsAppUserLinkId" IS NOT NULL;

        CREATE TABLE IF NOT EXISTS whatsapp_message_receipts (
            "Id" uuid NOT NULL,
            "ProviderMessageId" character varying(256) NOT NULL,
            "WhatsAppUserLinkId" uuid NOT NULL,
            "ChatSessionId" uuid,
            "ChatMessageId" uuid,
            "Direction" character varying(16) NOT NULL,
            "MessageType" character varying(32) NOT NULL,
            "Status" character varying(32) NOT NULL,
            "TextContent" character varying(4000),
            "MediaReference" character varying(16000),
            "ProviderOutboundMessageId" character varying(256),
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_whatsapp_message_receipts" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_whatsapp_receipts_links" FOREIGN KEY ("WhatsAppUserLinkId") REFERENCES whatsapp_user_links ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_whatsapp_receipts_sessions" FOREIGN KEY ("ChatSessionId") REFERENCES chat_sessions ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_whatsapp_receipts_messages" FOREIGN KEY ("ChatMessageId") REFERENCES chat_messages ("Id") ON DELETE SET NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_whatsapp_message_receipts_ProviderMessageId" ON whatsapp_message_receipts ("ProviderMessageId");
        CREATE INDEX IF NOT EXISTS "IX_whatsapp_message_receipts_WhatsAppUserLinkId_CreatedAt" ON whatsapp_message_receipts ("WhatsAppUserLinkId", "CreatedAt");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS whatsapp_message_receipts;
        DROP INDEX IF EXISTS "IX_chat_sessions_WhatsAppUserLinkId_Channel";
        ALTER TABLE chat_sessions DROP COLUMN IF EXISTS "WhatsAppUserLinkId";
        DROP TABLE IF EXISTS whatsapp_user_links;
        DROP TABLE IF EXISTS whatsapp_connections;
        ALTER TABLE chat_sessions DROP COLUMN IF EXISTS "Channel";
        """);
}
