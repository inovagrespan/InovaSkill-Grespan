using InovaSkill.Importer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Persistence;

public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<FileJob> FileJobs => Set<FileJob>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PreProcessorTemplate> PreProcessorTemplates => Set<PreProcessorTemplate>();
    public DbSet<PreProcessorTemplateRule> PreProcessorTemplateRules => Set<PreProcessorTemplateRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FilePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.NormalizedFilePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.LastHeartbeatAt).IsRequired();
            e.Property(x => x.CurrentStep).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.Status, x.LastHeartbeatAt });
        });

        modelBuilder.Entity<ImportError>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Column).HasMaxLength(128).IsRequired();
            e.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            e.Property(x => x.RecordIdentifier).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.FileJobId);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.SourceFileJobId);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.Sku);
            e.HasIndex(x => x.SourceFileJobId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
            e.Property(x => x.ProductSku).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.OrderNumber);
            e.HasIndex(x => x.SourceFileJobId);
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

