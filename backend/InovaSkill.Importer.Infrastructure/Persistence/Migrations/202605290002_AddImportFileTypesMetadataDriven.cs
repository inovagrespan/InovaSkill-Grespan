using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

public partial class AddImportFileTypesMetadataDriven : Migration
{
    private static readonly Guid SalesInvoiceId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CustomersId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProductsId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FinancialEntryId = new("44444444-4444-4444-4444-444444444444");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ImportFileTypes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                AllowedExtensions = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportFileTypes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ImportFileTypes_Code",
            table: "ImportFileTypes",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ImportFileTypes_IsActive",
            table: "ImportFileTypes",
            column: "IsActive");

        migrationBuilder.AddColumn<Guid>(
            name: "ImportFileTypeId",
            table: "ImportTemplatesV2",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ImportFileTypeCode",
            table: "FileJobs",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.InsertData(
            table: "ImportFileTypes",
            columns: new[] { "Id", "Code", "Name", "Description", "AllowedExtensions", "IsActive", "CreatedAt", "UpdatedAt" },
            values: new object[,]
            {
                { SalesInvoiceId, "SALES_INVOICE", "Nota Fiscal de Venda", "Importacao de notas fiscais de venda", ".csv,.xlsx", true, DateTime.UtcNow, DateTime.UtcNow },
                { CustomersId, "CUSTOMER_LIST", "Clientes", "Importacao da tabela de clientes", ".csv,.xlsx", true, DateTime.UtcNow, DateTime.UtcNow },
                { ProductsId, "PRODUCT_LIST", "Produtos", "Importacao da tabela de produtos", ".csv,.xlsx", true, DateTime.UtcNow, DateTime.UtcNow },
                { FinancialEntryId, "FINANCIAL_ENTRY", "Lancamento Financeiro", "Importacao de pedidos/lancamentos", ".csv,.xlsx", true, DateTime.UtcNow, DateTime.UtcNow }
            });

        migrationBuilder.Sql("""
            UPDATE "ImportTemplatesV2"
            SET "ImportFileTypeId" = CASE
                WHEN "FileType" = 1 THEN '22222222-2222-2222-2222-222222222222'::uuid
                WHEN "FileType" = 2 THEN '44444444-4444-4444-4444-444444444444'::uuid
                WHEN "FileType" = 3 THEN '33333333-3333-3333-3333-333333333333'::uuid
                WHEN "FileType" = 4 THEN '11111111-1111-1111-1111-111111111111'::uuid
                ELSE '33333333-3333-3333-3333-333333333333'::uuid
            END;
        """);

        migrationBuilder.AlterColumn<Guid>(
            name: "ImportFileTypeId",
            table: "ImportTemplatesV2",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ImportTemplatesV2_ImportFileTypeId",
            table: "ImportTemplatesV2",
            column: "ImportFileTypeId");

        migrationBuilder.AddForeignKey(
            name: "FK_ImportTemplatesV2_ImportFileTypes_ImportFileTypeId",
            table: "ImportTemplatesV2",
            column: "ImportFileTypeId",
            principalTable: "ImportFileTypes",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropColumn(
            name: "FileType",
            table: "ImportTemplatesV2");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FileType",
            table: "ImportTemplatesV2",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.DropForeignKey(
            name: "FK_ImportTemplatesV2_ImportFileTypes_ImportFileTypeId",
            table: "ImportTemplatesV2");

        migrationBuilder.DropIndex(
            name: "IX_ImportTemplatesV2_ImportFileTypeId",
            table: "ImportTemplatesV2");

        migrationBuilder.DropColumn(name: "ImportFileTypeId", table: "ImportTemplatesV2");
        migrationBuilder.DropColumn(name: "ImportFileTypeCode", table: "FileJobs");
        migrationBuilder.DropTable(name: "ImportFileTypes");
    }
}
