namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteImportCorrection
{
    public Guid Id { get; set; }
    public Guid DerivedImportId { get; set; }
    public RouteImport? DerivedImport { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid? SourceRouteId { get; set; }
    public Guid? SourceRouteEntryId { get; set; }
    public Guid? MunicipalityId { get; set; }
    public Guid? OriginalMunicipalityId { get; set; }
    public Guid? VehicleTypeId { get; set; }
    public string? SourceLabel { get; set; }
    public decimal? OriginalWeightKg { get; set; }
    public decimal? CorrectedWeightKg { get; set; }
    public decimal? OriginalCapacityKg { get; set; }
    public decimal? CorrectedCapacityKg { get; set; }
    public decimal? OriginalLatitude { get; set; }
    public decimal? OriginalLongitude { get; set; }
    public decimal? CorrectedLatitude { get; set; }
    public decimal? CorrectedLongitude { get; set; }
    public bool ExcludeFromOptimization { get; set; }
    public long RequestedByUserId { get; set; }
    public AppUser? RequestedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RouteImportAffectedWeekday
{
    public Guid ImportId { get; set; }
    public RouteImport? Import { get; set; }
    public string Weekday { get; set; } = string.Empty;
}

public sealed class MunicipalityAlias
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string NormalizedAlias { get; set; } = string.Empty;
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public long CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public long UpdatedByUserId { get; set; }
    public AppUser? UpdatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class RouteImportCorrectionKinds
{
    public const string SetWeight = "SET_WEIGHT";
    public const string ExcludeStop = "EXCLUDE_STOP";
    public const string LinkMunicipality = "LINK_MUNICIPALITY";
    public const string SetVehicleTypeCapacity = "SET_VEHICLE_TYPE_CAPACITY";
    public const string ConfirmOfficialCoordinate = "CONFIRM_OFFICIAL_COORDINATE";
    public const string SetManualCoordinate = "SET_MANUAL_COORDINATE";
}
