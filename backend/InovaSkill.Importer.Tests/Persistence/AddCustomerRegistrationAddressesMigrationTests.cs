using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddCustomerRegistrationAddressesMigrationTests
{
    [Fact]
    public void Up_CreatesCustomerAddressTableWithUniqueCustomerAndForeignKey()
    {
        var migration = new AddCustomerRegistrationAddresses();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("CREATE TABLE customer_registration_addresses", sql, StringComparison.Ordinal);
        Assert.Contains("UNIQUE (\"CustomerId\")", sql, StringComparison.Ordinal);
        Assert.Contains("REFERENCES customers", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("IX_customer_registration_addresses_Status", sql, StringComparison.Ordinal);
    }
}
