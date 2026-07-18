using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607180005_RemoveAlertsAndDetectionModule")]
public sealed class RemoveAlertsAndDetectionModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS finding_evidences;
        DROP TABLE IF EXISTS findings;
        DROP TABLE IF EXISTS detection_runs;
        DROP TABLE IF EXISTS detector_definitions;
        DROP TABLE IF EXISTS "Notifications";
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
