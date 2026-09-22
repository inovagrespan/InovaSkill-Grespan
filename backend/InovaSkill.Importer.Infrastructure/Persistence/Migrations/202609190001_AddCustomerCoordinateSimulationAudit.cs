using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609190001_AddCustomerCoordinateSimulationAudit")]
public class AddCustomerCoordinateSimulationAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE customer_coordinate_simulation_audits (
            "Id" uuid NOT NULL,
            "JobExecutionId" uuid NOT NULL,
            "CustomerRegistrationAddressId" uuid NOT NULL,
            "OriginalCoordinateExisted" boolean NOT NULL,
            "OriginalNormalizedAddress" character varying(1024),
            "OriginalSource" character varying(64),
            "OriginalStatus" character varying(32),
            "OriginalPrecision" character varying(32),
            "OriginalLatitude" numeric(9,6),
            "OriginalLongitude" numeric(9,6),
            "OriginalProviderPlaceId" character varying(512),
            "OriginalDisplayName" character varying(1024),
            "OriginalFailureReason" character varying(1024),
            "OriginalLastAttemptAt" timestamptz,
            "OriginalResolvedAt" timestamptz,
            "OriginalCreatedAt" timestamptz,
            "OriginalUpdatedAt" timestamptz,
            "BaseLatitude" numeric(9,6) NOT NULL,
            "BaseLongitude" numeric(9,6) NOT NULL,
            "BaseSource" character varying(64) NOT NULL,
            "SimulatedLatitude" numeric(9,6) NOT NULL,
            "SimulatedLongitude" numeric(9,6) NOT NULL,
            "ProviderPlaceId" character varying(512) NOT NULL,
            "DisplayName" character varying(1024) NOT NULL,
            "DistanceMeters" numeric(12,3) NOT NULL,
            "AppliedByUserId" bigint NOT NULL,
            "AppliedAt" timestamptz NOT NULL,
            "RevertJobExecutionId" uuid,
            "RevertedByUserId" bigint,
            "RevertedAt" timestamptz,
            CONSTRAINT "PK_customer_coordinate_simulation_audits" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_customer_coordinate_simulation_audits_job" FOREIGN KEY ("JobExecutionId")
                REFERENCES job_executions ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_customer_coordinate_simulation_audits_address" FOREIGN KEY ("CustomerRegistrationAddressId")
                REFERENCES customer_registration_addresses ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_customer_coordinate_simulation_audits_applied_user" FOREIGN KEY ("AppliedByUserId")
                REFERENCES app_users ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_customer_coordinate_simulation_audits_reverted_user" FOREIGN KEY ("RevertedByUserId")
                REFERENCES app_users ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_customer_coordinate_simulation_audits_revert_job" FOREIGN KEY ("RevertJobExecutionId")
                REFERENCES job_executions ("Id") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_customer_coordinate_simulation_audits_JobExecutionId"
            ON customer_coordinate_simulation_audits ("JobExecutionId");
        CREATE UNIQUE INDEX "IX_customer_coordinate_simulation_audits_active_address"
            ON customer_coordinate_simulation_audits ("CustomerRegistrationAddressId")
            WHERE "RevertedAt" IS NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS customer_coordinate_simulation_audits;
        """);
}
