using InovaSkill.Importer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Persistence;

public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<RouteImport> RouteImports => Set<RouteImport>();
    public DbSet<RouteImportError> RouteImportErrors => Set<RouteImportError>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<VehicleType> VehicleTypes => Set<VehicleType>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteEntry> RouteEntries => Set<RouteEntry>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerSnapshot> CustomerSnapshots => Set<CustomerSnapshot>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();
    public DbSet<FiscalDocumentItem> FiscalDocumentItems => Set<FiscalDocumentItem>();

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
                .HasForeignKey(x => x.RelatedEntityId).OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalCode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512).IsRequired();
            entity.HasIndex(x => new { x.DataSourceId, x.ExternalCode }).IsUnique();
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.RelatedLink).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.RelatedEntity).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
        });
    }
}
