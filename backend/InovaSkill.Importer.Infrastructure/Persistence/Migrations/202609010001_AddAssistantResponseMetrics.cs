using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609010001_AddAssistantResponseMetrics")]
public sealed class AddAssistantResponseMetrics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE ai_response_executions
            ADD COLUMN "QuestionMessageId" uuid NULL,
            ADD COLUMN "ResponseMessageId" uuid NULL,
            ADD COLUMN "Channel" varchar(16) NOT NULL DEFAULT 'web',
            ADD COLUMN "DurationMilliseconds" bigint NULL;

        ALTER TABLE ai_response_executions
            ADD CONSTRAINT "FK_ai_response_executions_question_message" FOREIGN KEY ("QuestionMessageId") REFERENCES chat_messages("Id") ON DELETE SET NULL,
            ADD CONSTRAINT "FK_ai_response_executions_response_message" FOREIGN KEY ("ResponseMessageId") REFERENCES chat_messages("Id") ON DELETE SET NULL;

        CREATE INDEX "IX_ai_response_executions_CreatedAt" ON ai_response_executions ("CreatedAt");
        CREATE INDEX "IX_ai_response_executions_QuestionMessageId" ON ai_response_executions ("QuestionMessageId");
        CREATE INDEX "IX_ai_response_executions_ResponseMessageId" ON ai_response_executions ("ResponseMessageId");

        UPDATE ai_response_executions
        SET "DurationMilliseconds" = GREATEST(0, FLOOR(EXTRACT(EPOCH FROM ("CompletedAt" - "CreatedAt")) * 1000)::bigint)
        WHERE "CompletedAt" IS NOT NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX IF EXISTS "IX_ai_response_executions_ResponseMessageId";
        DROP INDEX IF EXISTS "IX_ai_response_executions_QuestionMessageId";
        DROP INDEX IF EXISTS "IX_ai_response_executions_CreatedAt";
        ALTER TABLE ai_response_executions DROP CONSTRAINT IF EXISTS "FK_ai_response_executions_response_message";
        ALTER TABLE ai_response_executions DROP CONSTRAINT IF EXISTS "FK_ai_response_executions_question_message";
        ALTER TABLE ai_response_executions
            DROP COLUMN IF EXISTS "DurationMilliseconds",
            DROP COLUMN IF EXISTS "Channel",
            DROP COLUMN IF EXISTS "ResponseMessageId",
            DROP COLUMN IF EXISTS "QuestionMessageId";
        """);
}
