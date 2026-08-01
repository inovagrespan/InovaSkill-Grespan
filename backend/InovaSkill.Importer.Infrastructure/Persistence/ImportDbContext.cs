using InovaSkill.Importer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Persistence;

public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<RouteImport> RouteImports => Set<RouteImport>();
    public DbSet<RouteImportError> RouteImportErrors => Set<RouteImportError>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<VehicleType> VehicleTypes => Set<VehicleType>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteEntry> RouteEntries => Set<RouteEntry>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<MunicipalityCoordinate> MunicipalityCoordinates => Set<MunicipalityCoordinate>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerSnapshot> CustomerSnapshots => Set<CustomerSnapshot>();
    public DbSet<RouteCustomerAssignment> RouteCustomerAssignments => Set<RouteCustomerAssignment>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventorySnapshot> InventorySnapshots => Set<InventorySnapshot>();
    public DbSet<DailyInventoryRecord> DailyInventoryRecords => Set<DailyInventoryRecord>();
    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();
    public DbSet<FiscalDocumentItem> FiscalDocumentItems => Set<FiscalDocumentItem>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<RouteOptimizationRun> RouteOptimizationRuns => Set<RouteOptimizationRun>();
    public DbSet<RouteOptimizationScenario> RouteOptimizationScenarios => Set<RouteOptimizationScenario>();
    public DbSet<AiResponseExecution> AiResponseExecutions => Set<AiResponseExecution>();
    public DbSet<AiProviderCall> AiProviderCalls => Set<AiProviderCall>();
    public DbSet<AiModelPrice> AiModelPrices => Set<AiModelPrice>();
    public DbSet<AiConsumptionSettings> AiConsumptionSettings => Set<AiConsumptionSettings>();
    public DbSet<AiUserLimit> AiUserLimits => Set<AiUserLimit>();
    public DbSet<AiConsumptionAlert> AiConsumptionAlerts => Set<AiConsumptionAlert>();
    public DbSet<KnowledgeMemory> KnowledgeMemories => Set<KnowledgeMemory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(1024).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.ToTable("data_sources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProcessorKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ImportMode).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasOne(x => x.CurrentImport).WithMany()
                .HasForeignKey(x => x.CurrentImportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.LastSuccessfulImport).WithMany()
                .HasForeignKey(x => x.LastSuccessfulImportId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RouteImport>(entity =>
        {
            entity.ToTable("imports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            entity.Property(x => x.FilePath).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.FailureMessage).HasMaxLength(1024);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.DataSourceId, x.Version }).IsUnique();
            entity.HasOne(x => x.DataSource).WithMany(x => x.Imports)
                .HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RouteImportError>(entity =>
        {
            entity.ToTable("import_errors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SheetName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Field).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RawValue).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.CorrectedValue).HasMaxLength(512);
            entity.HasIndex(x => new { x.ImportId, x.Status });
            entity.HasIndex(x => new { x.ImportId, x.SheetName, x.RowNumber, x.Field });
            entity.HasOne(x => x.Import).WithMany(x => x.Errors)
                .HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobExecution>(entity =>
        {
            entity.ToTable("job_executions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.JobType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1024);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasOne(x => x.Import).WithMany(x => x.JobExecutions)
                .HasForeignKey(x => x.RelatedEntityId).OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);
        });

        modelBuilder.Entity<RouteOptimizationRun>(entity =>
        {
            entity.ToTable("route_optimization_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.RequestedFrom).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ProgressStage).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.AlgorithmVersion).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RulesVersion).HasMaxLength(64).IsRequired();
            entity.Property(x => x.InputHash).HasMaxLength(64);
            entity.Property(x => x.ErrorCode).HasMaxLength(64);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1024);
            entity.Property(x => x.ProgressPercentage).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.Scope, x.ReferenceDate, x.Status });
            entity.HasIndex(x => new { x.TargetRouteId, x.ReferenceDate, x.CreatedAt });
            entity.HasIndex(x => x.InputHash);
            entity.HasOne(x => x.TargetRoute).WithMany()
                .HasForeignKey(x => x.TargetRouteId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.SnapshotImport).WithMany()
                .HasForeignKey(x => x.SnapshotImportId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RouteOptimizationScenario>(entity =>
        {
            entity.ToTable("route_optimization_scenarios");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Score).HasPrecision(12, 4);
            entity.Property(x => x.EstimatedDistanceChangeKm).HasPrecision(12, 2);
            entity.Property(x => x.CurrentMetricsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ProposedMetricsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.WarningsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ReasonsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CityReallocationsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.TruckChangeJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.RunId, x.Rank }).IsUnique();
            entity.HasOne(x => x.Run).WithMany(x => x.Scenarios)
                .HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("chat_sessions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.UpdatedAt });
            entity.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            entity.HasIndex(x => new { x.ChatSessionId, x.CreatedAt });
            entity.HasOne(x => x.ChatSession).WithMany(x => x.Messages)
                .HasForeignKey(x => x.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiResponseExecution>(entity =>
        {
            entity.ToTable("ai_response_executions"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AiProviderCall>(entity =>
        {
            entity.ToTable("ai_provider_calls"); entity.HasKey(x => x.Id);
            entity.Property(x => x.ProviderResponseId).HasMaxLength(128);
            entity.Property(x => x.Model).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.InputPricePerMillionUsd).HasPrecision(18, 8);
            entity.Property(x => x.OutputPricePerMillionUsd).HasPrecision(18, 8);
            entity.Property(x => x.InputCostUsd).HasPrecision(18, 8);
            entity.Property(x => x.OutputCostUsd).HasPrecision(18, 8);
            entity.HasIndex(x => new { x.ResponseExecutionId, x.CreatedAt });
            entity.HasIndex(x => new { x.Model, x.CreatedAt });
            entity.HasOne(x => x.ResponseExecution).WithMany(x => x.Calls).HasForeignKey(x => x.ResponseExecutionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AiModelPrice>(entity =>
        {
            entity.ToTable("ai_model_prices"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Model).HasMaxLength(128).IsRequired();
            entity.Property(x => x.InputPricePerMillionUsd).HasPrecision(18, 8);
            entity.Property(x => x.OutputPricePerMillionUsd).HasPrecision(18, 8);
            entity.HasIndex(x => new { x.Model, x.EffectiveFrom }).IsUnique();
        });
        modelBuilder.Entity<AiConsumptionSettings>(entity =>
        {
            entity.ToTable("ai_consumption_settings"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Model).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DefaultAlertPercentage).HasPrecision(5, 2);
        });
        modelBuilder.Entity<AiUserLimit>(entity =>
        {
            entity.ToTable("ai_user_limits"); entity.HasKey(x => x.UserId);
            entity.Property(x => x.AlertPercentage).HasPrecision(5, 2);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AiConsumptionAlert>(entity =>
        {
            entity.ToTable("ai_consumption_alerts"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.PeriodMonth, x.Level }).IsUnique();
            entity.HasIndex(x => new { x.ReadAt, x.CreatedAt });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeMemory>(entity =>
        {
            entity.ToTable("knowledge_memories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.EmbeddingJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.Scope, x.OwnerUserId, x.IsActive, x.UpdatedAt });
            entity.HasIndex(x => new { x.Subject, x.Scope, x.OwnerUserId, x.IsActive });
            entity.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SourceChatMessage).WithMany().HasForeignKey(x => x.SourceChatMessageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SupersedesMemory).WithMany().HasForeignKey(x => x.SupersedesMemoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VehicleType>(entity =>
        {
            entity.ToTable("vehicle_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CapacityKg).HasPrecision(12, 2);
            entity.Property(x => x.CapacityVolumeM3).HasPrecision(12, 3);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Route>(entity =>
        {
            entity.ToTable("routes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Weekday).HasMaxLength(16).IsRequired();
            entity.Property(x => x.TotalWeightKg).HasPrecision(18, 3);
            entity.Property(x => x.TotalVolumeM3).HasPrecision(18, 3);
            entity.Property(x => x.WeightOccupancy).HasPrecision(12, 6);
            entity.Property(x => x.VolumeOccupancy).HasPrecision(12, 6);
            entity.Property(x => x.PalletOccupancy).HasPrecision(12, 6);
            entity.Property(x => x.OverallOccupancy).HasPrecision(12, 6);
            entity.Property(x => x.OccupancyStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.ImportId, x.Weekday, x.Name });
            entity.HasIndex(x => new { x.ImportId, x.OverallOccupancy });
            entity.HasOne(x => x.Import).WithMany(x => x.Routes)
                .HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.VehicleType).WithMany(x => x.Routes)
                .HasForeignKey(x => x.VehicleTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RouteEntry>(entity =>
        {
            entity.ToTable("route_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.AveragePerDay).HasPrecision(18, 3);
            entity.Property(x => x.Note).HasMaxLength(2000);
            entity.HasIndex(x => new { x.RouteId, x.Sequence });
            entity.HasOne(x => x.Route).WithMany(x => x.Entries)
                .HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Municipality).WithMany()
                .HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Municipality>(entity =>
        {
            entity.ToTable("municipalities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StateCode).HasMaxLength(2).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.IbgeCode).HasMaxLength(7);
            entity.HasIndex(x => new { x.StateCode, x.NormalizedName }).IsUnique();
        });

        modelBuilder.Entity<MunicipalityCoordinate>(entity =>
        {
            entity.ToTable("municipality_coordinates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
            entity.Property(x => x.Source).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.FailureReason).HasMaxLength(1024);
            entity.HasIndex(x => x.MunicipalityId).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.Municipality).WithOne(x => x.Coordinate)
                .HasForeignKey<MunicipalityCoordinate>(x => x.MunicipalityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BranchCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExternalCode).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.DataSourceId, x.BranchCode, x.ExternalCode }).IsUnique();
            entity.HasIndex(x => new { x.DataSourceId, x.ExternalCode, x.BranchCode });
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerSnapshot>(entity =>
        {
            entity.ToTable("customer_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(16).IsRequired();
            entity.Property(x => x.LegalName).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TradeName).HasMaxLength(512).IsRequired();
            entity.Property(x => x.CustomerType).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.ImportId, x.CustomerId }).IsUnique();
            entity.HasIndex(x => new { x.ImportId, x.MunicipalityId });
            entity.HasIndex(x => new { x.ImportId, x.CustomerType });
            entity.HasOne(x => x.Import).WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Customer).WithMany(x => x.Snapshots).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RouteCustomerAssignment>(entity =>
        {
            entity.ToTable("route_customer_assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.RouteId, x.CustomerId }).IsUnique();
            entity.HasIndex(x => new { x.RouteId, x.Source });
            entity.HasIndex(x => new { x.CustomerId, x.Source });
            entity.HasIndex(x => new { x.RouteId, x.MunicipalityId });
            entity.HasOne(x => x.Route).WithMany(x => x.CustomerAssignments)
                .HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Customer).WithMany(x => x.RouteAssignments)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Municipality).WithMany()
                .HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512).IsRequired();
            entity.Property(x => x.ErpCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.OperationalCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            entity.Property(x => x.GroupCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.NetWeightKg).HasPrecision(18, 6);
            entity.Property(x => x.GrossWeightKg).HasPrecision(18, 6);
            entity.Property(x => x.Gtin).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.ErpCode).IsUnique();
            entity.HasIndex(x => x.OperationalCode);
            entity.HasIndex(x => x.GroupCode);
            entity.HasIndex(x => new { x.DataSourceId, x.ExternalCode }).IsUnique();
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        modelBuilder.Entity<InventorySnapshot>(entity =>
        {
            entity.ToTable("inventory_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BranchCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.WarehouseCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.OnHandQuantity).HasPrecision(18, 6);
            entity.Property(x => x.CommittedQuantity).HasPrecision(18, 6);
            entity.Property(x => x.AvailableQuantity).HasPrecision(18, 6);
            entity.Property(x => x.StockValue).HasPrecision(18, 2);
            entity.Property(x => x.CommittedValue).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.ImportId, x.ProductId, x.BranchCode, x.WarehouseCode }).IsUnique();
            entity.HasIndex(x => new { x.ImportId, x.AvailableQuantity });
            entity.HasIndex(x => x.ProductId);
            entity.HasOne(x => x.Import).WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DailyInventoryRecord>(entity =>
        {
            entity.ToTable("daily_inventory_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductionQuantity).HasPrecision(18, 6);
            entity.Property(x => x.OutboundQuantity).HasPrecision(18, 6);
            entity.Property(x => x.AdjustmentQuantity).HasPrecision(18, 6);
            entity.Property(x => x.ClosingQuantity).HasPrecision(18, 6);
            entity.Property(x => x.FirstShiftProductionQuantity).HasPrecision(18, 6);
            entity.Property(x => x.SecondShiftProductionQuantity).HasPrecision(18, 6);
            entity.Property(x => x.ThirdShiftProductionQuantity).HasPrecision(18, 6);
            entity.Property(x => x.SourceSheetName).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.ImportId, x.ProductId, x.Date }).IsUnique();
            entity.HasIndex(x => new { x.ProductId, x.Date });
            entity.HasIndex(x => x.Date);
            entity.HasOne(x => x.Import).WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FiscalDocument>(entity =>
        {
            entity.ToTable("fiscal_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentNumber).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Series).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.MovementType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CustomerCodeAtIssue).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BranchCodeAtIssue).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CustomerNameAtIssue).HasMaxLength(512).IsRequired();
            entity.Property(x => x.CityNameAtIssue).HasMaxLength(256).IsRequired();
            entity.Property(x => x.StateCodeAtIssue).HasMaxLength(2).IsRequired();
            entity.Property(x => x.OperationCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.OperationDescription).HasMaxLength(256).IsRequired();
            entity.Property(x => x.MovementCategory).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.OriginalDocumentNumber).HasMaxLength(128);
            entity.HasIndex(x => new
            {
                x.DataSourceId, x.DocumentType, x.DocumentNumber, x.Series, x.IssueDate,
                x.CustomerCodeAtIssue, x.BranchCodeAtIssue
            }).IsUnique();
            entity.HasIndex(x => new { x.CustomerId, x.IssueDate, x.MovementCategory });
            entity.HasIndex(x => new { x.IssueDate, x.MovementCategory });
            entity.HasIndex(x => x.DocumentNumber);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.FirstSeenImport).WithMany().HasForeignKey(x => x.FirstSeenImportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.LastSeenImport).WithMany().HasForeignKey(x => x.LastSeenImportId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FiscalDocumentItem>(entity =>
        {
            entity.ToTable("fiscal_document_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ItemNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProductCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(512).IsRequired();
            entity.Property(x => x.ProductGroupCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ProductGroupDescription).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 6);
            entity.Property(x => x.GrossWeightKg).HasPrecision(18, 3);
            entity.Property(x => x.UnitValue).HasPrecision(18, 6);
            entity.Property(x => x.SourceTotalValue).HasPrecision(18, 2);
            entity.Property(x => x.Expenses).HasPrecision(18, 2);
            entity.Property(x => x.Ipi).HasPrecision(18, 2);
            entity.Property(x => x.Icms).HasPrecision(18, 2);
            entity.Property(x => x.Iss).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.FiscalDocumentId, x.ItemNumber }).IsUnique();
            entity.HasIndex(x => x.ProductId);
            entity.HasOne(x => x.FiscalDocument).WithMany(x => x.Items)
                .HasForeignKey(x => x.FiscalDocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

    }
}
