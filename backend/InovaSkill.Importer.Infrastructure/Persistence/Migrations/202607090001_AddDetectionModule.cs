using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607090001_AddDetectionModule")]
public sealed class AddDetectionModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS detector_definitions (
            "Id" uuid PRIMARY KEY,
            "Code" varchar(64) NOT NULL,
            "Name" varchar(256) NOT NULL,
            "Description" varchar(1024),
            "Status" varchar(32) NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_detector_definitions_Code" ON detector_definitions ("Code");

        CREATE TABLE IF NOT EXISTS detection_runs (
            "Id" uuid PRIMARY KEY,
            "DetectorDefinitionId" uuid NOT NULL REFERENCES detector_definitions("Id") ON DELETE RESTRICT,
            "Status" varchar(32) NOT NULL,
            "Trigger" varchar(32) NOT NULL,
            "RequestedAt" timestamptz NOT NULL,
            "StartedAt" timestamptz,
            "FinishedAt" timestamptz,
            "AttemptCount" integer NOT NULL DEFAULT 0,
            "AnalyzedItems" integer NOT NULL DEFAULT 0,
            "FindingsCount" integer NOT NULL DEFAULT 0,
            "StatusReason" varchar(1024),
            "RequestedByUserId" uuid,
            "RetryOfRunId" uuid REFERENCES detection_runs("Id") ON DELETE SET NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_detection_runs_DetectorDefinitionId_Status" ON detection_runs ("DetectorDefinitionId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_detection_runs_RequestedAt" ON detection_runs ("RequestedAt");

        CREATE TABLE IF NOT EXISTS findings (
            "Id" uuid PRIMARY KEY,
            "DetectionRunId" uuid NOT NULL REFERENCES detection_runs("Id") ON DELETE CASCADE,
            "Fingerprint" varchar(256) NOT NULL,
            "Title" varchar(512) NOT NULL,
            "Description" varchar(2000) NOT NULL,
            "SubjectType" varchar(128) NOT NULL,
            "SubjectId" varchar(128) NOT NULL,
            "SubjectLabel" varchar(512),
            "DetectedAt" timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_findings_DetectionRunId" ON findings ("DetectionRunId");
        CREATE INDEX IF NOT EXISTS "IX_findings_SubjectType_SubjectId" ON findings ("SubjectType", "SubjectId");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_findings_DetectionRunId_Fingerprint" ON findings ("DetectionRunId", "Fingerprint");

        CREATE TABLE IF NOT EXISTS finding_evidences (
            "Id" uuid PRIMARY KEY,
            "FindingId" uuid NOT NULL REFERENCES findings("Id") ON DELETE CASCADE,
            "Name" varchar(256) NOT NULL,
            "Value" varchar(512) NOT NULL,
            "ReferenceValue" varchar(512),
            "Unit" varchar(32),
            "Description" varchar(1024),
            "SourceType" varchar(128),
            "SourceId" varchar(128),
            "ObservedAt" timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_finding_evidences_FindingId" ON finding_evidences ("FindingId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS finding_evidences;
        DROP TABLE IF EXISTS findings;
        DROP TABLE IF EXISTS detection_runs;
        DROP TABLE IF EXISTS detector_definitions;
        """);
}
