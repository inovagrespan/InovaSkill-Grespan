using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608010002_AddKnowledgeMemories")]
public sealed class AddKnowledgeMemories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "knowledge_memories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                SourceChatMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                SupersedesMemoryId = table.Column<Guid>(type: "uuid", nullable: true),
                Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                EmbeddingJson = table.Column<string>(type: "jsonb", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_memories", x => x.Id);
                table.CheckConstraint("CK_knowledge_memories_scope_owner", "(\"Scope\" = 'company' AND \"OwnerUserId\" IS NULL) OR (\"Scope\" = 'user' AND \"OwnerUserId\" IS NOT NULL)");
                table.ForeignKey("FK_knowledge_memories_app_users_CreatedByUserId", x => x.CreatedByUserId, "app_users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_knowledge_memories_app_users_OwnerUserId", x => x.OwnerUserId, "app_users", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_knowledge_memories_chat_messages_SourceChatMessageId", x => x.SourceChatMessageId, "chat_messages", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_knowledge_memories_knowledge_memories_SupersedesMemoryId", x => x.SupersedesMemoryId, "knowledge_memories", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_knowledge_memories_scope_owner_active_updated", "knowledge_memories", new[] { "Scope", "OwnerUserId", "IsActive", "UpdatedAt" });
        migrationBuilder.CreateIndex("IX_knowledge_memories_subject_scope_owner_active", "knowledge_memories", new[] { "Subject", "Scope", "OwnerUserId", "IsActive" });
        migrationBuilder.CreateIndex("IX_knowledge_memories_CreatedByUserId", "knowledge_memories", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_knowledge_memories_SourceChatMessageId", "knowledge_memories", "SourceChatMessageId");
        migrationBuilder.CreateIndex("IX_knowledge_memories_SupersedesMemoryId", "knowledge_memories", "SupersedesMemoryId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("knowledge_memories");
}
