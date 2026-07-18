using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607180001_AddChatSessions")]
public sealed class AddChatSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS chat_sessions (
            "Id" uuid NOT NULL,
            "UserId" bigint NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_chat_sessions" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_chat_sessions_app_users_UserId" FOREIGN KEY ("UserId") REFERENCES app_users ("Id") ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS chat_messages (
            "Id" uuid NOT NULL,
            "ChatSessionId" uuid NOT NULL,
            "Role" character varying(32) NOT NULL,
            "Content" character varying(4000) NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_chat_messages" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_chat_messages_chat_sessions_ChatSessionId" FOREIGN KEY ("ChatSessionId") REFERENCES chat_sessions ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_chat_sessions_UserId_UpdatedAt" ON chat_sessions ("UserId", "UpdatedAt");
        CREATE INDEX IF NOT EXISTS "IX_chat_messages_ChatSessionId_CreatedAt" ON chat_messages ("ChatSessionId", "CreatedAt");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS chat_messages;
        DROP TABLE IF EXISTS chat_sessions;
        """);
}
