using InovaSkill.Importer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Persistence;

public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<FileJob> FileJobs => Set<FileJob>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<ProcessingStepExecution> ProcessingStepExecutions => Set<ProcessingStepExecution>();
    public DbSet<ProcessingJobLog> ProcessingJobLogs => Set<ProcessingJobLog>();
    public DbSet<ProcessingJobEventLog> ProcessingJobEventLogs => Set<ProcessingJobEventLog>();
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CommercialTransaction> CommercialTransactions => Set<CommercialTransaction>();
    public DbSet<SalesSummaryDaily> SalesSummariesDaily => Set<SalesSummaryDaily>();
    public DbSet<SalesSummaryWeekly> SalesSummariesWeekly => Set<SalesSummaryWeekly>();
    public DbSet<CustomerSummaryDaily> CustomerSummariesDaily => Set<CustomerSummaryDaily>();
    public DbSet<CustomerSummaryWeekly> CustomerSummariesWeekly => Set<CustomerSummaryWeekly>();
    public DbSet<CustomerSummaryMonthly> CustomerSummariesMonthly => Set<CustomerSummaryMonthly>();
    public DbSet<ImportFileType> ImportFileTypes => Set<ImportFileType>();
    public DbSet<ImportTemplate> ImportTemplates => Set<ImportTemplate>();
    public DbSet<ImportColumnMapping> ImportColumnMappings => Set<ImportColumnMapping>();
    public DbSet<TransformRule> TransformRules => Set<TransformRule>();
    public DbSet<ColumnMappingTransformRule> ColumnMappingTransformRules => Set<ColumnMappingTransformRule>();
    public DbSet<PreProcessorTemplate> PreProcessorTemplates => Set<PreProcessorTemplate>();
    public DbSet<PreProcessorTemplateRule> PreProcessorTemplateRules => Set<PreProcessorTemplateRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FilePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.OriginalFileName).HasMaxLength(512).IsRequired();
            e.Property(x => x.NormalizedFilePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.ImportFileTypeCode).HasMaxLength(64);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.LastHeartbeatAt).IsRequired();
            e.Property(x => x.StartedAt);
            e.Property(x => x.FinishedAt);
            e.Property(x => x.LockedBy).HasMaxLength(128).IsRequired();
            e.Property(x => x.LockedAt);
            e.Property(x => x.CurrentStep).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.Status, x.LastHeartbeatAt });
            e.HasIndex(x => x.LockedAt);
        });

        modelBuilder.Entity<ImportError>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Stage).HasMaxLength(64).IsRequired();
            e.Property(x => x.Column).HasMaxLength(128).IsRequired();
            e.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            e.Property(x => x.RecordIdentifier).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.FileJobId);
            e.HasIndex(x => new { x.FileJobId, x.Stage });
        });

        modelBuilder.Entity<ProcessingStepExecution>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Step).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.FileJobId);
            e.HasIndex(x => new { x.Step, x.StartedAt });
        });

        modelBuilder.Entity<ProcessingJobLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Stage).HasMaxLength(64).IsRequired();
            e.Property(x => x.Level).HasMaxLength(32).IsRequired();
            e.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.FileJobId, x.Timestamp });
        });

        modelBuilder.Entity<ProcessingJobEventLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.ErrorMessage).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.FileJobId, x.EventType, x.CorrelationId });
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<WorkerHeartbeat>(e =>
        {
            e.HasKey(x => x.WorkerId);
            e.Property(x => x.WorkerId).HasMaxLength(128).IsRequired();
            e.Property(x => x.CurrentTask).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.LastSeenAt);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CustomerCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.CustomerCode).IsUnique();
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.SourceFileJobId);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.Sku).IsUnique();
            e.HasIndex(x => x.SourceFileJobId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductSku).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.OrderNumber, x.CustomerEmail, x.ProductSku, x.OrderedAt }).IsUnique();
            e.HasIndex(x => x.SourceFileJobId);
        });

        modelBuilder.Entity<CommercialTransaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DocumentNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.ProductDescription).HasMaxLength(512).IsRequired();
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TransactionType).HasMaxLength(128).IsRequired();
            e.Property(x => x.City).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductGroup).HasMaxLength(128).IsRequired();
            e.Property(x => x.GrossWeightKg).HasColumnType("decimal(18,3)");
            e.HasIndex(x => x.DocumentNumber);
            e.HasIndex(x => x.SourceFileJobId);
            e.HasIndex(x => x.TransactionDate);
            e.HasIndex(x => x.CustomerName);
            e.HasIndex(x => x.ProductCode);
            e.HasIndex(x => x.City);
            e.HasIndex(x => new { x.SourceFileJobId, x.TransactionDate });
            e.HasIndex(x => new
            {
                x.DocumentNumber,
                x.TransactionDate,
                x.CustomerCode,
                x.ProductCode,
                x.TransactionType,
                x.City,
                x.ProductGroup,
                x.Quantity,
                x.UnitPrice,
                x.GrossWeightKg
            }).IsUnique();
        });

        modelBuilder.Entity<SalesSummaryDaily>(e =>
        {
            e.ToTable("SalesSummariesDaily");
            e.HasKey(x => x.Id);
            e.Property(x => x.ReferenceDate).IsRequired();
            e.Property(x => x.City).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductGroup).HasMaxLength(128).IsRequired();
            e.Property(x => x.TransactionType).HasMaxLength(128).IsRequired();
            e.Property(x => x.TotalQuantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalGrossWeightKg).HasColumnType("decimal(18,3)");
            e.Property(x => x.ProcessedAt).IsRequired();
            e.HasIndex(x => x.SourceFileJobId);
            e.HasIndex(x => x.ReferenceDate);
            e.HasIndex(x => new { x.ReferenceDate, x.City, x.ProductGroup, x.TransactionType });
            e.HasIndex(x => new { x.SourceFileJobId, x.ReferenceDate, x.City, x.ProductGroup, x.TransactionType })
                .IsUnique();
        });

        modelBuilder.Entity<SalesSummaryWeekly>(e =>
        {
            e.ToTable("SalesSummariesWeekly");
            e.HasKey(x => x.Id);
            e.Property(x => x.WeekStartDate).IsRequired();
            e.Property(x => x.City).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductGroup).HasMaxLength(128).IsRequired();
            e.Property(x => x.TransactionType).HasMaxLength(128).IsRequired();
            e.Property(x => x.TotalQuantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalGrossWeightKg).HasColumnType("decimal(18,3)");
            e.Property(x => x.ProcessedAt).IsRequired();
            e.HasIndex(x => x.SourceFileJobId);
            e.HasIndex(x => x.WeekStartDate);
            e.HasIndex(x => new { x.WeekStartDate, x.City, x.ProductGroup, x.TransactionType });
            e.HasIndex(x => new { x.SourceFileJobId, x.WeekStartDate, x.City, x.ProductGroup, x.TransactionType })
                .IsUnique();
        });

        modelBuilder.Entity<CustomerSummaryDaily>(e =>
        {
            e.ToTable("CustomerSummariesDaily");
            e.HasKey(x => x.Id);
            e.Property(x => x.ReferenceDate).IsRequired();
            e.Property(x => x.CustomerCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(256).IsRequired();
            e.Property(x => x.City).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductGroup).HasMaxLength(128).IsRequired();
            e.Property(x => x.TransactionType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Revenue).HasColumnType("decimal(18,2)");
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.Weight).HasColumnType("decimal(18,3)");
            e.Property(x => x.ProcessedAt).IsRequired();
            e.HasIndex(x => x.SourceFileJobId);
            e.HasIndex(x => x.ReferenceDate);
            e.HasIndex(x => x.CustomerName);
            e.HasIndex(x => new { x.ReferenceDate, x.CustomerName });
            e.HasIndex(x => new { x.SourceFileJobId, x.ReferenceDate, x.CustomerCode, x.City, x.ProductGroup, x.TransactionType })
                .IsUnique();
        });

        modelBuilder.Entity<CustomerSummaryWeekly>(e =>
        {
            e.ToTable("CustomerSummariesWeekly");
            e.HasKey(x => x.Id);
            e.Property(x => x.WeekStartDate).IsRequired();
            e.Property(x => x.CustomerCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(256).IsRequired();
            e.Property(x => x.City).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductGroup).HasMaxLength(128).IsRequired();
            e.Property(x => x.TransactionType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Revenue).HasColumnType("decimal(18,2)");
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.Weight).HasColumnType("decimal(18,3)");
            e.Property(x => x.ProcessedAt).IsRequired();
            e.HasIndex(x => x.SourceFileJobId);
            e.HasIndex(x => x.WeekStartDate);
            e.HasIndex(x => x.CustomerName);
            e.HasIndex(x => new { x.WeekStartDate, x.CustomerName });
            e.HasIndex(x => new { x.SourceFileJobId, x.WeekStartDate, x.CustomerCode, x.City, x.ProductGroup, x.TransactionType })
                .IsUnique();
        });

        modelBuilder.Entity<CustomerSummaryMonthly>(e =>
        {
            e.ToTable("CustomerSummariesMonthly");
            e.HasKey(x => x.Id);
            e.Property(x => x.MonthStartDate).IsRequired();
            e.Property(x => x.CustomerCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(256).IsRequired();
            e.Property(x => x.City).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductGroup).HasMaxLength(128).IsRequired();
            e.Property(x => x.TransactionType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Revenue).HasColumnType("decimal(18,2)");
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.Weight).HasColumnType("decimal(18,3)");
            e.Property(x => x.ProcessedAt).IsRequired();
            e.HasIndex(x => x.SourceFileJobId);
            e.HasIndex(x => x.MonthStartDate);
            e.HasIndex(x => x.CustomerName);
            e.HasIndex(x => new { x.MonthStartDate, x.CustomerName });
            e.HasIndex(x => new { x.SourceFileJobId, x.MonthStartDate, x.CustomerCode, x.City, x.ProductGroup, x.TransactionType })
                .IsUnique();
        });

        modelBuilder.Entity<ImportFileType>(e =>
        {
            e.ToTable("ImportFileTypes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.AllowedExtensions).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<ImportTemplate>(e =>
        {
            e.ToTable("ImportTemplatesV2");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.FileNamePattern).HasMaxLength(256).IsRequired();
            e.Property(x => x.RequiredHeadersCsv).HasMaxLength(2048).IsRequired();
            e.HasIndex(x => x.IsActive);
            e.HasIndex(x => x.ImportFileTypeId);
            e.HasOne(x => x.ImportFileType)
                .WithMany(x => x.ImportTemplates)
                .HasForeignKey(x => x.ImportFileTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportColumnMapping>(e =>
        {
            e.ToTable("ImportColumnMappings");
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceColumnName).HasMaxLength(256).IsRequired();
            e.Property(x => x.TargetFieldName).HasMaxLength(256).IsRequired();
            e.Property(x => x.DefaultValue).HasMaxLength(4000);
            e.HasIndex(x => new { x.ImportTemplateId, x.TargetFieldName });
            e.HasOne(x => x.ImportTemplate)
                .WithMany(x => x.ColumnMappings)
                .HasForeignKey(x => x.ImportTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransformRule>(e =>
        {
            e.ToTable("TransformRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ColumnMappingTransformRule>(e =>
        {
            e.ToTable("ColumnMappingTransformRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Order).IsRequired();
            e.Property(x => x.ParametersJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ImportColumnMappingId, x.Order });
            e.HasOne(x => x.ImportColumnMapping)
                .WithMany(x => x.TransformRules)
                .HasForeignKey(x => x.ImportColumnMappingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TransformRule)
                .WithMany(x => x.ColumnMappings)
                .HasForeignKey(x => x.TransformRuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PreProcessorTemplate>(e =>
        {
            e.ToTable("ImportTemplates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.FileNamePattern).HasMaxLength(256).IsRequired();
            e.Property(x => x.RequiredHeadersCsv).HasMaxLength(2048).IsRequired();
            e.Property(x => x.ColumnMappingsJson).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ValidationRulesJson).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.IsActive, x.FileType });
        });

        modelBuilder.Entity<PreProcessorTemplateRule>(e =>
        {
            e.ToTable("ImportTemplateRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.PreProcessorTemplateId).HasColumnName("ImportTemplateId");
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.RuleType).HasMaxLength(64).IsRequired();
            e.Property(x => x.ConfigJson).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.PreProcessorTemplateId, x.SortOrder });
            e.HasOne(x => x.PreProcessorTemplate)
                .WithMany(x => x.Rules)
                .HasForeignKey(x => x.PreProcessorTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
