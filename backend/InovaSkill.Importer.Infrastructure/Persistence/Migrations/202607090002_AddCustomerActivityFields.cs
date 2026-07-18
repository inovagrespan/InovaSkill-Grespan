using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607090002_AddCustomerActivityFields")]
public sealed class AddCustomerActivityFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customers
        ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;

        ALTER TABLE customers
        ADD COLUMN IF NOT EXISTS "LastPurchaseAt" timestamptz;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customers DROP COLUMN IF EXISTS "LastPurchaseAt";
        ALTER TABLE customers DROP COLUMN IF EXISTS "IsActive";
        """);
}
