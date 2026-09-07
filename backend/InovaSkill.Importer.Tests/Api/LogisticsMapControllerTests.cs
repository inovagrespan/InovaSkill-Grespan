using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class LogisticsMapControllerTests
{
    [Fact]
    public async Task Customers_ReturnsMunicipalityFallbackAndCountsOnlyRowsWithoutCoordinates()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"logistics-map-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "clientes.xlsx",
            FilePath = "clientes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        source.CurrentImportId = import.Id;
        var marilia = Municipality("Marília", "MARILIA");
        var pending = Municipality("Sem Coordenada", "SEM COORDENADA");
        var firstCustomer = Customer(source.Id, "0001", "01");
        var secondCustomer = Customer(source.Id, "0002", "01");
        db.AddRange(source, import, marilia, pending, firstCustomer, secondCustomer);
        await db.SaveChangesAsync();
        db.MunicipalityCoordinates.Add(new MunicipalityCoordinate {
            Id = Guid.NewGuid(), MunicipalityId = marilia.Id, Latitude = -22.2171m,
            Longitude = -49.9501m, Source = "test", Status = MunicipalityCoordinateStatuses.Resolved,
            CreatedAt = now, UpdatedAt = now, ResolvedAt = now
        });
        db.CustomerSnapshots.AddRange(
            Snapshot(import.Id, firstCustomer.Id, marilia.Id, "Padaria Real"),
            Snapshot(import.Id, secondCustomer.Id, pending.Id, "Mercado Pendente"));
        await db.SaveChangesAsync();

        var result = await new LogisticsMapController(db).Customers(null, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"total\":2", json);
        Assert.Contains("\"visible\":1", json);
        Assert.Contains("\"withoutCoordinates\":1", json);
        Assert.Contains("Padaria Real", json);
        Assert.Contains("\"locationPrecision\":\"MUNICIPALITY\"", json);
        Assert.Contains("\"coordinateAccuracy\":\"APPROXIMATE\"", json);
        Assert.Contains("\"coordinatePrecision\":\"MUNICIPALITY\"", json);
        Assert.DoesNotContain("Mercado Pendente", json);
    }

    [Fact]
    public async Task Customers_ExposesInterpolatedAddressCoordinateAsApproximate()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"logistics-map-interpolated-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2,
            Active = true, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "c.xlsx",
            FilePath = "c.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now };
        source.CurrentImportId = import.Id;
        var city = Municipality("Marília", "MARILIA");
        var customer = Customer(source.Id, "0001", "01");
        var address = new CustomerRegistrationAddress { Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "1",
            Source = "HERE_IMPORT", Status = CustomerRegistrationAddressStatuses.Resolved, Street = "Rua A", Number = "100",
            City = "Marília", StateCode = "SP", CreatedAt = now, UpdatedAt = now };
        db.AddRange(source, import, city, customer, address,
            new CustomerAddressCoordinate { Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id,
                NormalizedAddress = "RUA A|100", Source = "HERE_IMPORT", Status = CustomerAddressCoordinateStatuses.Resolved,
                Precision = CustomerAddressCoordinatePrecisions.Interpolated, Latitude = -22.2m, Longitude = -49.9m,
                CreatedAt = now, UpdatedAt = now },
            Snapshot(import.Id, customer.Id, city.Id, "Cliente interpolado"));
        await db.SaveChangesAsync();

        var result = await new LogisticsMapController(db).Customers(null, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"total\":1", json);
        Assert.Contains("\"visible\":1", json);
        Assert.Contains("\"withoutCoordinates\":0", json);
        Assert.Contains("Cliente interpolado", json);
        Assert.Contains("\"locationPrecision\":\"ADDRESS_INTERPOLATED\"", json);
        Assert.Contains("\"coordinateAccuracy\":\"APPROXIMATE\"", json);
        Assert.Contains("\"coordinatePrecision\":\"INTERPOLATED\"", json);
    }

    [Fact]
    public async Task Customers_PrefersResolvedAddressCoordinateOverMunicipalityCoordinate()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"logistics-map-address-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2,
            Active = true, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "c.xlsx",
            FilePath = "c.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now };
        source.CurrentImportId = import.Id;
        var city = Municipality("Marília", "MARILIA");
        var customer = Customer(source.Id, "0001", "01");
        var address = new CustomerRegistrationAddress { Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "1",
            Source = "BRASIL_API", Status = CustomerRegistrationAddressStatuses.Resolved, Street = "Rua São Luiz",
            Number = "100", Neighborhood = "Centro", City = "Marília", StateCode = "SP", PostalCode = "17500-001",
            CreatedAt = now, UpdatedAt = now };
        db.AddRange(source, import, city, customer, address,
            new MunicipalityCoordinate { Id = Guid.NewGuid(), MunicipalityId = city.Id, Latitude = -22.2171m, Longitude = -49.9501m,
                Source = "CSV", Status = MunicipalityCoordinateStatuses.Resolved, CreatedAt = now, UpdatedAt = now },
            new CustomerAddressCoordinate { Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id,
                NormalizedAddress = "RUA A", Source = "GEOAPIFY_EXACT", Status = CustomerAddressCoordinateStatuses.Resolved,
                Latitude = -22.200000m, Longitude = -49.900000m, CreatedAt = now, UpdatedAt = now },
            Snapshot(import.Id, customer.Id, city.Id, "Cliente exato"));
        await db.SaveChangesAsync();

        var result = await new LogisticsMapController(db).Customers(null, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"locationPrecision\":\"ADDRESS_EXACT\"", json);
        Assert.Contains("\"coordinateAccuracy\":\"EXACT\"", json);
        Assert.Contains("\"coordinatePrecision\":\"EXACT\"", json);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("Rua São Luiz, 100 - Centro - Marília/SP - CEP 17500-001",
            document.RootElement.GetProperty("items")[0].GetProperty("address").GetString());
        Assert.Contains("\"lat\":-22.2", json);
        Assert.Contains("\"municipalityLat\":-22.2171", json);

        var coordinate = await db.CustomerAddressCoordinates.SingleAsync();
        coordinate.Source = "BRASIL_API_POSTAL_CODE";
        await db.SaveChangesAsync();
        var approximateResult = await new LogisticsMapController(db).Customers(null, CancellationToken.None);
        var approximateJson = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(approximateResult).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"visible\":1", approximateJson);
        Assert.Contains("\"withoutCoordinates\":0", approximateJson);
        Assert.Contains("Cliente exato", approximateJson);
        Assert.Contains("\"locationPrecision\":\"ADDRESS_APPROXIMATE\"", approximateJson);
        Assert.Contains("\"coordinateAccuracy\":\"APPROXIMATE\"", approximateJson);
        Assert.Contains("\"coordinatePrecision\":\"POSTAL_CODE\"", approximateJson);
    }

    [Theory]
    [InlineData("GEOAPIFY_STREET", "EXACT", "STREET")]
    [InlineData("BRASIL_API_POSTAL_CODE", "EXACT", "POSTAL_CODE")]
    [InlineData("GEOAPIFY_MUNICIPALITY", "EXACT", "MUNICIPALITY")]
    public async Task Customers_ExposesApproximateAddressCoordinates(
        string coordinateSource,
        string persistedPrecision,
        string expectedPrecision)
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"logistics-map-approximate-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2,
            Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "c.xlsx",
            FilePath = "c.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        source.CurrentImportId = import.Id;
        var city = Municipality("Marília", "MARILIA");
        var customer = Customer(source.Id, "0001", "01");
        var address = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "1", Source = "BRASIL_API",
            Status = CustomerRegistrationAddressStatuses.Resolved, Street = "Rua A", Number = "100",
            City = "Marília", StateCode = "SP", CreatedAt = now, UpdatedAt = now
        };
        db.AddRange(source, import, city, customer, address,
            new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id, NormalizedAddress = "RUA A|100",
                Source = coordinateSource, Status = CustomerAddressCoordinateStatuses.Resolved,
                Precision = persistedPrecision, Latitude = -22.2m, Longitude = -49.9m,
                CreatedAt = now, UpdatedAt = now
            },
            Snapshot(import.Id, customer.Id, city.Id, "Cliente aproximado"));
        await db.SaveChangesAsync();

        var result = await new LogisticsMapController(db).Customers(null, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"total\":1", json);
        Assert.Contains("\"visible\":1", json);
        Assert.Contains("\"withoutCoordinates\":0", json);
        Assert.Contains("Cliente aproximado", json);
        Assert.Contains("\"locationPrecision\":\"ADDRESS_APPROXIMATE\"", json);
        Assert.Contains("\"coordinateAccuracy\":\"APPROXIMATE\"", json);
        Assert.Contains($"\"coordinatePrecision\":\"{expectedPrecision}\"", json);
    }

    [Fact]
    public async Task Customers_SpreadsCoordinatesResolvedOnlyAtMunicipalityLevelWithinSameCity()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"logistics-map-municipality-spread-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2,
            Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "c.xlsx",
            FilePath = "c.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        source.CurrentImportId = import.Id;
        var city = Municipality("Marília", "MARILIA");
        var firstCustomer = Customer(source.Id, "0001", "01");
        var secondCustomer = Customer(source.Id, "0002", "01");
        var firstAddress = RegistrationAddress(firstCustomer.Id, now);
        var secondAddress = RegistrationAddress(secondCustomer.Id, now);
        db.AddRange(source, import, city, firstCustomer, secondCustomer, firstAddress, secondAddress,
            AddressCoordinate(firstAddress.Id, "GEOAPIFY_MUNICIPALITY", "EXACT", -22.2171m, -49.9501m, now),
            AddressCoordinate(secondAddress.Id, "NOMINATIM_MUNICIPALITY", "EXACT", -22.2171m, -49.9501m, now),
            Snapshot(import.Id, firstCustomer.Id, city.Id, "Cliente municipal 1"),
            Snapshot(import.Id, secondCustomer.Id, city.Id, "Cliente municipal 2"));
        await db.SaveChangesAsync();

        var result = await new LogisticsMapController(db).Customers(null, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(2, items.Length);
        Assert.All(items, item =>
        {
            Assert.Equal("ADDRESS_APPROXIMATE", item.GetProperty("locationPrecision").GetString());
            Assert.Equal("MUNICIPALITY", item.GetProperty("coordinatePrecision").GetString());
        });
        var firstPosition = (items[0].GetProperty("lat").GetDouble(), items[0].GetProperty("lng").GetDouble());
        var secondPosition = (items[1].GetProperty("lat").GetDouble(), items[1].GetProperty("lng").GetDouble());
        Assert.NotEqual(firstPosition, secondPosition);
    }

    [Fact]
    public async Task Customers_MixedPrecisionsPreserveCountIdentityAndActiveFilterInvariants()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"logistics-map-mixed-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2,
            Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "c.xlsx",
            FilePath = "c.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        source.CurrentImportId = import.Id;
        var resolvedCity = Municipality("Marília", "MARILIA");
        var unresolvedCity = Municipality("Sem Coordenada", "SEM COORDENADA");
        var activeExact = Customer(source.Id, "0001", "01", isActive: true);
        var activeStreet = Customer(source.Id, "0002", "01", isActive: true);
        var activeMunicipalityFallback = Customer(source.Id, "0003", "01", isActive: true);
        var activeWithoutCoordinate = Customer(source.Id, "0004", "01", isActive: true);
        var inactiveInterpolated = Customer(source.Id, "0005", "01", isActive: false);
        var inactivePostalCode = Customer(source.Id, "0006", "01", isActive: false);
        var inactiveWithoutCoordinate = Customer(source.Id, "0007", "01", isActive: false);
        var exactAddress = RegistrationAddress(activeExact.Id, now);
        var streetAddress = RegistrationAddress(activeStreet.Id, now);
        var interpolatedAddress = RegistrationAddress(inactiveInterpolated.Id, now);
        var postalCodeAddress = RegistrationAddress(inactivePostalCode.Id, now);

        db.AddRange(source, import, resolvedCity, unresolvedCity,
            activeExact, activeStreet, activeMunicipalityFallback, activeWithoutCoordinate,
            inactiveInterpolated, inactivePostalCode, inactiveWithoutCoordinate,
            exactAddress, streetAddress, interpolatedAddress, postalCodeAddress,
            new MunicipalityCoordinate
            {
                Id = Guid.NewGuid(), MunicipalityId = resolvedCity.Id, Latitude = -22.2171m, Longitude = -49.9501m,
                Source = "CSV", Status = MunicipalityCoordinateStatuses.Resolved, CreatedAt = now, UpdatedAt = now
            },
            AddressCoordinate(exactAddress.Id, "GEOAPIFY_EXACT", "EXACT", -22.20m, -49.90m, now),
            AddressCoordinate(streetAddress.Id, "GEOAPIFY_STREET", "EXACT", -22.21m, -49.91m, now),
            AddressCoordinate(interpolatedAddress.Id, "HERE_IMPORT", "INTERPOLATED", -22.22m, -49.92m, now),
            AddressCoordinate(postalCodeAddress.Id, "BRASIL_API_POSTAL_CODE", "EXACT", -22.23m, -49.93m, now),
            Snapshot(import.Id, activeExact.Id, resolvedCity.Id, "Ativo exato"),
            Snapshot(import.Id, activeStreet.Id, resolvedCity.Id, "Ativo por rua"),
            Snapshot(import.Id, activeMunicipalityFallback.Id, resolvedCity.Id, "Ativo municipal"),
            Snapshot(import.Id, activeWithoutCoordinate.Id, unresolvedCity.Id, "Ativo sem coordenada"),
            Snapshot(import.Id, inactiveInterpolated.Id, resolvedCity.Id, "Inativo interpolado"),
            Snapshot(import.Id, inactivePostalCode.Id, resolvedCity.Id, "Inativo por CEP"),
            Snapshot(import.Id, inactiveWithoutCoordinate.Id, unresolvedCity.Id, "Inativo sem coordenada"));
        await db.SaveChangesAsync();

        var controller = new LogisticsMapController(db);
        var allResult = await controller.Customers(null, CancellationToken.None);
        var activeResult = await controller.Customers("true", CancellationToken.None);
        var inactiveResult = await controller.Customers("false", CancellationToken.None);

        AssertPayload(allResult, total: 7, visible: 5, expectedActive: null,
            [activeExact.Id, activeStreet.Id, activeMunicipalityFallback.Id, inactiveInterpolated.Id, inactivePostalCode.Id]);
        AssertPayload(activeResult, total: 4, visible: 3, expectedActive: true,
            [activeExact.Id, activeStreet.Id, activeMunicipalityFallback.Id]);
        AssertPayload(inactiveResult, total: 3, visible: 2, expectedActive: false,
            [inactiveInterpolated.Id, inactivePostalCode.Id]);
    }

    private static void AssertPayload(
        ActionResult result,
        int total,
        int visible,
        bool? expectedActive,
        IReadOnlyCollection<Guid> expectedIds)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        var withoutCoordinates = root.GetProperty("withoutCoordinates").GetInt32();
        var ids = items.Select(item => item.GetProperty("id").GetGuid()).ToArray();

        Assert.Equal(total, root.GetProperty("total").GetInt32());
        Assert.Equal(visible, root.GetProperty("visible").GetInt32());
        Assert.Equal(visible, items.Length);
        Assert.Equal(total, visible + withoutCoordinates);
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(expectedIds.Order(), ids.Order());
        if (expectedActive.HasValue)
            Assert.All(items, item => Assert.Equal(expectedActive.Value, item.GetProperty("isActive").GetBoolean()));
    }

    private static Municipality Municipality(string name, string normalizedName) => new()
    {
        Id = Guid.NewGuid(),
        StateCode = "SP",
        Name = name,
        NormalizedName = normalizedName,
        CreatedAt = DateTime.UtcNow
    };

    private static Customer Customer(Guid sourceId, string code, string branch, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        DataSourceId = sourceId,
        BranchCode = branch,
        ExternalCode = code,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow
    };

    private static CustomerRegistrationAddress RegistrationAddress(Guid customerId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        DocumentNumber = customerId.ToString("N"),
        Source = "BRASIL_API",
        Status = CustomerRegistrationAddressStatuses.Resolved,
        Street = "Rua A",
        Number = "100",
        City = "Marília",
        StateCode = "SP",
        CreatedAt = now,
        UpdatedAt = now
    };

    private static CustomerAddressCoordinate AddressCoordinate(
        Guid registrationAddressId,
        string source,
        string precision,
        decimal latitude,
        decimal longitude,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        CustomerRegistrationAddressId = registrationAddressId,
        NormalizedAddress = registrationAddressId.ToString("N"),
        Source = source,
        Status = CustomerAddressCoordinateStatuses.Resolved,
        Precision = precision,
        Latitude = latitude,
        Longitude = longitude,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static CustomerSnapshot Snapshot(
        Guid importId,
        Guid customerId,
        Guid municipalityId,
        string tradeName) => new()
    {
        Id = Guid.NewGuid(),
        ImportId = importId,
        CustomerId = customerId,
        MunicipalityId = municipalityId,
        DocumentNumber = "",
        DocumentType = "UNKNOWN",
        LegalName = tradeName,
        TradeName = tradeName,
        CustomerType = "Mercado",
        SourceRowNumber = 1,
        CreatedAt = DateTime.UtcNow
    };
}
