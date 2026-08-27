using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608180001_AddWhatsAppFloodControl")]
public sealed class AddWhatsAppFloodControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE whatsapp_user_links
            ADD COLUMN IF NOT EXISTS "FloodBlockedUntil" timestamptz;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE whatsapp_user_links
            DROP COLUMN IF EXISTS "FloodBlockedUntil";
        """);
}
