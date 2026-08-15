using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608110003_AddGenericJobEngine")]
public sealed class AddGenericJobEngine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE job_schedules (
            "Id" uuid NOT NULL,
            "Name" character varying(128) NOT NULL,
            "JobType" character varying(64) NOT NULL,
            "ContractVersion" integer NOT NULL DEFAULT 1,
            "ParametersJson" jsonb NOT NULL DEFAULT '{}'::jsonb,
            "CronExpression" character varying(128) NOT NULL,
            "TimeZoneId" character varying(64) NOT NULL DEFAULT 'America/Sao_Paulo',
            "IsActive" boolean NOT NULL DEFAULT true,
            "CreatedByUserId" bigint NOT NULL,
            "UpdatedByUserId" bigint NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            "NextExecutionAt" timestamptz,
            CONSTRAINT "PK_job_schedules" PRIMARY KEY ("Id")
        );
        CREATE INDEX "IX_job_schedules_IsActive_NextExecutionAt"
            ON job_schedules ("IsActive", "NextExecutionAt");

        ALTER TABLE job_executions
            ADD COLUMN "ContractVersion" integer NOT NULL DEFAULT 1,
            ADD COLUMN "Queue" character varying(64) NOT NULL DEFAULT 'default',
            ADD COLUMN "Trigger" character varying(16) NOT NULL DEFAULT 'System',
            ADD COLUMN "ParametersJson" jsonb NOT NULL DEFAULT '{}'::jsonb,
            ADD COLUMN "ResultJson" jsonb,
            ADD COLUMN "ProgressPercent" numeric NOT NULL DEFAULT 0,
            ADD COLUMN "ProgressMessage" character varying(512),
            ADD COLUMN "CancellationRequestedAt" timestamptz,
            ADD COLUMN "RequestedByUserId" bigint,
            ADD COLUMN "ScheduleId" uuid,
            ADD COLUMN "RetriedFromJobExecutionId" uuid;

        UPDATE job_executions SET
            "Queue" = CASE
                WHEN "JobType" = 'PROCESS_IMPORT' THEN 'imports'
                WHEN "JobType" = 'PROCESS_ROUTE_OPTIMIZATION' THEN 'route-optimization'
                ELSE 'default' END,
            "Trigger" = CASE WHEN "JobType" = 'PROCESS_IMPORT' THEN 'Import' ELSE 'System' END,
            "ParametersJson" = CASE
                WHEN "JobType" = 'PROCESS_IMPORT' THEN jsonb_build_object('importId', "RelatedEntityId")
                WHEN "JobType" = 'MUNICIPALITY_COORDINATE_ENRICHMENT' THEN jsonb_build_object('importId', "RelatedEntityId", 'reprocessFailed', false)
                WHEN "JobType" = 'PROCESS_ROUTE_OPTIMIZATION' THEN jsonb_build_object('optimizationRunId', "RelatedEntityId")
                WHEN "JobType" = 'WHATSAPP_MESSAGE_PROCESSING' THEN jsonb_build_object('receiptId', "RelatedEntityId")
                ELSE jsonb_build_object('relatedEntityId', "RelatedEntityId") END;

        CREATE INDEX "IX_job_executions_ScheduleId" ON job_executions ("ScheduleId");
        CREATE INDEX "IX_job_executions_RetriedFromJobExecutionId" ON job_executions ("RetriedFromJobExecutionId");
        ALTER TABLE job_executions ADD CONSTRAINT "FK_job_executions_job_schedules_ScheduleId"
            FOREIGN KEY ("ScheduleId") REFERENCES job_schedules ("Id") ON DELETE SET NULL;
        ALTER TABLE job_executions ADD CONSTRAINT "FK_job_executions_job_executions_RetriedFromJobExecutionId"
            FOREIGN KEY ("RetriedFromJobExecutionId") REFERENCES job_executions ("Id") ON DELETE SET NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE job_executions DROP CONSTRAINT IF EXISTS "FK_job_executions_job_executions_RetriedFromJobExecutionId";
        ALTER TABLE job_executions DROP CONSTRAINT IF EXISTS "FK_job_executions_job_schedules_ScheduleId";
        DROP INDEX IF EXISTS "IX_job_executions_RetriedFromJobExecutionId";
        DROP INDEX IF EXISTS "IX_job_executions_ScheduleId";
        ALTER TABLE job_executions
            DROP COLUMN IF EXISTS "RetriedFromJobExecutionId",
            DROP COLUMN IF EXISTS "ScheduleId",
            DROP COLUMN IF EXISTS "RequestedByUserId",
            DROP COLUMN IF EXISTS "CancellationRequestedAt",
            DROP COLUMN IF EXISTS "ProgressMessage",
            DROP COLUMN IF EXISTS "ProgressPercent",
            DROP COLUMN IF EXISTS "ResultJson",
            DROP COLUMN IF EXISTS "ParametersJson",
            DROP COLUMN IF EXISTS "Trigger",
            DROP COLUMN IF EXISTS "Queue",
            DROP COLUMN IF EXISTS "ContractVersion";
        DROP TABLE IF EXISTS job_schedules;
        """);
}
