using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

public partial class AddImportTemplateMappingArchitecture : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ImportTemplatesV2",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                FileNamePattern = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                RequiredHeadersCsv = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                FileType = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportTemplatesV2", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TransformRules",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransformRules", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ImportColumnMappings",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ImportTemplateId = table.Column<long>(type: "bigint", nullable: false),
                SourceColumnName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                TargetFieldName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                DefaultValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportColumnMappings", x => x.Id);
                table.ForeignKey(
                    name: "FK_ImportColumnMappings_ImportTemplatesV2_ImportTemplateId",
                    column: x => x.ImportTemplateId,
                    principalTable: "ImportTemplatesV2",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ColumnMappingTransformRules",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ImportColumnMappingId = table.Column<long>(type: "bigint", nullable: false),
                TransformRuleId = table.Column<long>(type: "bigint", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                ParametersJson = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ColumnMappingTransformRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_ColumnMappingTransformRules_ImportColumnMappings_ImportColumnMappingId",
                    column: x => x.ImportColumnMappingId,
                    principalTable: "ImportColumnMappings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ColumnMappingTransformRules_TransformRules_TransformRuleId",
                    column: x => x.TransformRuleId,
                    principalTable: "TransformRules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ImportTemplatesV2_IsActive",
            table: "ImportTemplatesV2",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_TransformRules_Code",
            table: "TransformRules",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ImportColumnMappings_ImportTemplateId_TargetFieldName",
            table: "ImportColumnMappings",
            columns: new[] { "ImportTemplateId", "TargetFieldName" });

        migrationBuilder.CreateIndex(
            name: "IX_ColumnMappingTransformRules_ImportColumnMappingId_Order",
            table: "ColumnMappingTransformRules",
            columns: new[] { "ImportColumnMappingId", "Order" });

        migrationBuilder.CreateIndex(
            name: "IX_ColumnMappingTransformRules_TransformRuleId",
            table: "ColumnMappingTransformRules",
            column: "TransformRuleId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ColumnMappingTransformRules");
        migrationBuilder.DropTable(name: "ImportColumnMappings");
        migrationBuilder.DropTable(name: "TransformRules");
        migrationBuilder.DropTable(name: "ImportTemplatesV2");
    }
}
