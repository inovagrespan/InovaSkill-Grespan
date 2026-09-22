using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class CustomerCoordinateSimulationMigrationTests
{
    [Fact]
    public void Migration_DefinesAuditForeignKeysAndActiveAddressIndex()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestMigration().Apply(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("customer_coordinate_simulation_audits", sql);
        Assert.Contains("JobExecutionId", sql);
        Assert.Contains("AppliedByUserId", sql);
        Assert.Contains("RevertedAt\" IS NULL", sql);
        Assert.Contains("IX_customer_coordinate_simulation_audits_active_address", sql);
    }

    [Fact]
    public void ProviderSourceMigration_PreservesExistingGoogleAudits()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestProviderSourceMigration().Apply(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("SimulatedSource", sql);
        Assert.Contains("GOOGLE_SIMULATED_NEARBY", sql);
        Assert.Contains("DROP DEFAULT", sql);
    }

    [Fact]
    public void CreatedAddressMigration_DefaultsExistingAuditsToFalse()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestCreatedAddressMigration().Apply(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("RegistrationAddressCreatedBySimulation", sql);
        Assert.Contains("DEFAULT false", sql);
        Assert.Contains("DROP DEFAULT", sql);
    }

    [Fact]
    public void CustomerAuditMigration_AllowsTechnicalAddressRemovalWithoutLosingHistory()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestCustomerAuditMigration().Apply(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("CustomerId", sql);
        Assert.Contains("DROP NOT NULL", sql);
        Assert.Contains("ON DELETE SET NULL", sql);
    }

    [Fact]
    public void ActiveCoordinateMigration_PreventsDuplicateSimulatedPoints()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestActiveCoordinateMigration().Apply(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("IX_customer_coordinate_simulation_audits_active_coordinate", sql);
        Assert.Contains("SimulatedLatitude", sql);
        Assert.Contains("SimulatedLongitude", sql);
        Assert.Contains("RevertedAt\" IS NULL", sql);
    }

    private sealed class TestMigration : AddCustomerCoordinateSimulationAudit
    {
        public void Apply(MigrationBuilder builder) => base.Up(builder);
    }

    private sealed class TestProviderSourceMigration : AddSimulationProviderSource
    {
        public void Apply(MigrationBuilder builder) => base.Up(builder);
    }

    private sealed class TestCreatedAddressMigration : TrackSimulationCreatedAddress
    {
        public void Apply(MigrationBuilder builder) => base.Up(builder);
    }

    private sealed class TestCustomerAuditMigration : PreserveSimulationAuditCustomer
    {
        public void Apply(MigrationBuilder builder) => base.Up(builder);
    }

    private sealed class TestActiveCoordinateMigration : UniqueActiveSimulationCoordinates
    {
        public void Apply(MigrationBuilder builder) => base.Up(builder);
    }
}
