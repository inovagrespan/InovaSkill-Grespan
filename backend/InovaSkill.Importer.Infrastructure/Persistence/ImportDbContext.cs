using InovaSkill.Importer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Persistence;

public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<FileJob> FileJobs => Set<FileJob>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<ProcessingStepExecution> ProcessingStepExecutions => Set<ProcessingStepExecution>();
    public DbSet<ProcessingJobLog> ProcessingJobLogs => Set<ProcessingJobLog>();
    public DbSet<ProcessingJobEventLog> ProcessingJobEventLogs => Set<ProcessingJobEventLog>();
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<RoutePlanningImport> RoutePlanningImports => Set<RoutePlanningImport>();
    public DbSet<RoutePlan> RoutePlans => Set<RoutePlan>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<TruckCapacityProfile> TruckCapacityProfiles => Set<TruckCapacityProfile>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CommercialTransaction> CommercialTransactions => Set<CommercialTransaction>();
    public DbSet<SalesSummaryDaily> SalesSummariesDaily => Set<SalesSummaryDaily>();
    public DbSet<SalesSummaryWeekly> SalesSummariesWeekly => Set<SalesSummaryWeekly>();
    public DbSet<CustomerSummaryDaily> CustomerSummariesDaily => Set<CustomerSummaryDaily>();
    public DbSet<CustomerSummaryWeekly> CustomerSummariesWeekly => Set<CustomerSummaryWeekly>();
    public DbSet<CustomerSummaryMonthly> CustomerSummariesMonthly => Set<CustomerSummaryMonthly>();
    public DbSet<ClienteIndicador> ClienteIndicadores => Set<ClienteIndicador>();
    public DbSet<ClienteForecast> ClienteForecasts => Set<ClienteForecast>();
    public DbSet<AiAlert> AiAlerts => Set<AiAlert>();
    public DbSet<AiAlertStatusHistory> AiAlertStatusHistory => Set<AiAlertStatusHistory>();
    public DbSet<AiAlertNotificationHistory> AiAlertNotificationHistory => Set<AiAlertNotificationHistory>();
    public DbSet<AiAlertEscalationHistory> AiAlertEscalationHistory => Set<AiAlertEscalationHistory>();
    public DbSet<ImportFileType> ImportFileTypes => Set<ImportFileType>();
    public DbSet<ImportTemplate> ImportTemplates => Set<ImportTemplate>();
    public DbSet<ImportColumnMapping> ImportColumnMappings => Set<ImportColumnMapping>();
    public DbSet<TransformRule> TransformRules => Set<TransformRule>();
    public DbSet<ColumnMappingTransformRule> ColumnMappingTransformRules => Set<ColumnMappingTransformRule>();
    public DbSet<PreProcessorTemplate> PreProcessorTemplates => Set<PreProcessorTemplate>();
    public DbSet<PreProcessorTemplateRule> PreProcessorTemplateRules => Set<PreProcessorTemplateRule>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingComment> MeetingComments => Set<MeetingComment>();
    public DbSet<MeetingProblem> MeetingProblems => Set<MeetingProblem>();
    public DbSet<MeetingQuestion> MeetingQuestions => Set<MeetingQuestion>();
    public DbSet<MeetingAnswer> MeetingAnswers => Set<MeetingAnswer>();
    public DbSet<MeetingAiAnalysis> MeetingAiAnalyses => Set<MeetingAiAnalysis>();
    public DbSet<MeetingDecision> MeetingDecisions => Set<MeetingDecision>();
    public DbSet<MeetingAction> MeetingActions => Set<MeetingAction>();
    public DbSet<MeetingHistory> MeetingHistories => Set<MeetingHistory>();
    public DbSet<CriticalPending> CriticalPendencies => Set<CriticalPending>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(e =>
        {
            e.ToTable("Jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(128).IsRequired();
            e.Property(x => x.CurrentStep).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.Property(x => x.Error).HasMaxLength(4000).IsRequired();
            e.Property(x => x.LockedBy).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.Type, x.Status, x.CreatedAt });
            e.HasIndex(x => x.LockedAt);
        });

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
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.SourceFileJobId);
        });

        modelBuilder.Entity<RoutePlanningImport>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceFileJobId).IsRequired();
            e.Property(x => x.SourceFileName).HasMaxLength(512).IsRequired();
            e.Property(x => x.ImportedAt).IsRequired();
            e.HasIndex(x => x.SourceFileJobId).IsUnique();
            e.HasIndex(x => x.ImportedAt);
        });

        modelBuilder.Entity<RoutePlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SheetName).HasMaxLength(128).IsRequired();
            e.Property(x => x.WeekdayLabel).HasMaxLength(64).IsRequired();
            e.Property(x => x.RouteName).HasMaxLength(256).IsRequired();
            e.Property(x => x.VehicleType).HasMaxLength(64).IsRequired();
            e.Property(x => x.VehicleCapacityKg).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalAverageLoadKg).HasColumnType("decimal(18,3)");
            e.Property(x => x.OccupancyPercent).HasColumnType("decimal(9,2)");
            e.HasIndex(x => new { x.RoutePlanningImportId, x.WeekdayOrder, x.RouteOrder });
            e.HasIndex(x => new { x.WeekdayOrder, x.RouteName });
            e.HasOne(x => x.RoutePlanningImport)
                .WithMany(x => x.Routes)
                .HasForeignKey(x => x.RoutePlanningImportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RouteStop>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DestinationName).HasMaxLength(256).IsRequired();
            e.Property(x => x.DeliveriesRaw).HasMaxLength(64).IsRequired();
            e.Property(x => x.AverageLoadKg).HasColumnType("decimal(18,3)");
            e.Property(x => x.Note).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.RoutePlanId, x.StopOrder });
            e.HasIndex(x => x.DestinationName);
            e.HasOne(x => x.RoutePlan)
                .WithMany(x => x.Stops)
                .HasForeignKey(x => x.RoutePlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TruckCapacityProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.VehicleType).HasMaxLength(64).IsRequired();
            e.Property(x => x.CapacityKg).HasColumnType("decimal(18,2)");
            e.HasIndex(x => new { x.RoutePlanningImportId, x.VehicleType }).IsUnique();
            e.HasOne(x => x.RoutePlanningImport)
                .WithMany(x => x.TruckCapacities)
                .HasForeignKey(x => x.RoutePlanningImportId)
                .OnDelete(DeleteBehavior.Cascade);
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
            e.Property(x => x.SupplierCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.SupplierName).HasMaxLength(256).IsRequired();
            e.Property(x => x.RouteName).HasMaxLength(256).IsRequired();
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
            e.HasIndex(x => x.SupplierName);
            e.HasIndex(x => x.RouteName);
            e.HasIndex(x => x.ProductCode);
            e.HasIndex(x => x.ProductDescription);
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

        modelBuilder.Entity<ClienteIndicador>(e =>
        {
            e.ToTable("ClienteIndicadores");
            e.HasKey(x => x.Id);
            e.Property(x => x.ClienteId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Faturamento3M).HasColumnType("decimal(18,2)");
            e.Property(x => x.Faturamento6M).HasColumnType("decimal(18,2)");
            e.Property(x => x.Faturamento12M).HasColumnType("decimal(18,2)");
            e.Property(x => x.Crescimento3M).HasColumnType("decimal(9,2)");
            e.Property(x => x.Crescimento6M).HasColumnType("decimal(9,2)");
            e.Property(x => x.Crescimento12M).HasColumnType("decimal(9,2)");
            e.Property(x => x.MediaMovel3M).HasColumnType("decimal(18,2)");
            e.Property(x => x.MediaMovel6M).HasColumnType("decimal(18,2)");
            e.Property(x => x.MediaMovel12M).HasColumnType("decimal(18,2)");
            e.Property(x => x.FrequenciaCompra).HasColumnType("decimal(18,2)");
            e.Property(x => x.TicketMedioGeral).HasColumnType("decimal(18,2)");
            e.Property(x => x.Tendencia).HasMaxLength(64).IsRequired();
            e.Property(x => x.Classificacao).HasMaxLength(16).IsRequired();
            e.Property(x => x.AtualizadoEm).IsRequired();
            e.HasIndex(x => x.ClienteId).IsUnique();
            e.HasIndex(x => x.ScorePotencial);
            e.HasIndex(x => x.Tendencia);
            e.HasIndex(x => x.Classificacao);
        });

        modelBuilder.Entity<ClienteForecast>(e =>
        {
            e.ToTable("ClienteForecasts");
            e.HasKey(x => x.Id);
            e.Property(x => x.ClienteId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Previsao30Dias).HasColumnType("decimal(18,2)");
            e.Property(x => x.Previsao60Dias).HasColumnType("decimal(18,2)");
            e.Property(x => x.Previsao90Dias).HasColumnType("decimal(18,2)");
            e.Property(x => x.TendenciaPrevista).HasMaxLength(64).IsRequired();
            e.Property(x => x.ErroMedioHistorico).HasColumnType("decimal(18,2)");
            e.Property(x => x.ConfiancaModelo).HasColumnType("decimal(9,2)");
            e.Property(x => x.UltimaObservacao).IsRequired();
            e.Property(x => x.AtualizadoEm).IsRequired();
            e.HasIndex(x => x.ClienteId).IsUnique();
            e.HasIndex(x => x.TendenciaPrevista);
        });

        modelBuilder.Entity<AiAlert>(e =>
        {
            e.ToTable("AiAlerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ResponsibleArea).HasMaxLength(64).IsRequired();
            e.Property(x => x.ResponsibleManager).HasMaxLength(256).IsRequired();
            e.Property(x => x.InvolvedAreasCsv).HasMaxLength(512).IsRequired();
            e.Property(x => x.InvolvedUsersCsv).HasMaxLength(1024).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(64).IsRequired();
            e.Property(x => x.Origin).HasMaxLength(64).IsRequired();
            e.Property(x => x.EvidenceJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.ExpectedImpact).HasMaxLength(2000).IsRequired();
            e.Property(x => x.AiSuggestion).HasMaxLength(4000).IsRequired();
            e.Property(x => x.RelatedTasksCsv).HasMaxLength(1024).IsRequired();
            e.Property(x => x.LinkedDecision).HasMaxLength(2000).IsRequired();
            e.Property(x => x.CancellationReason).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.ResponsibleArea, x.Status, x.Severity });
            e.HasIndex(x => x.ResponseDeadlineAt);
            e.HasIndex(x => x.ActionDeadlineAt);
            e.HasIndex(x => x.EscalatedAt);
        });

        modelBuilder.Entity<AiAlertStatusHistory>(e =>
        {
            e.ToTable("AiAlertStatusHistory");
            e.HasKey(x => x.Id);
            e.Property(x => x.PreviousStatus).HasMaxLength(64).IsRequired();
            e.Property(x => x.NewStatus).HasMaxLength(64).IsRequired();
            e.Property(x => x.ChangedBy).HasMaxLength(256).IsRequired();
            e.Property(x => x.Justification).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.AiAlertId, x.ChangedAt });
            e.HasOne(x => x.AiAlert)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.AiAlertId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiAlertNotificationHistory>(e =>
        {
            e.ToTable("AiAlertNotificationHistory");
            e.HasKey(x => x.Id);
            e.Property(x => x.Recipient).HasMaxLength(256).IsRequired();
            e.Property(x => x.Channel).HasMaxLength(64).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.AiAlertId, x.SentAt });
            e.HasOne(x => x.AiAlert)
                .WithMany(x => x.NotificationHistory)
                .HasForeignKey(x => x.AiAlertId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiAlertEscalationHistory>(e =>
        {
            e.ToTable("AiAlertEscalationHistory");
            e.HasKey(x => x.Id);
            e.Property(x => x.FromRecipient).HasMaxLength(256).IsRequired();
            e.Property(x => x.ToRecipient).HasMaxLength(256).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.AiAlertId, x.EscalatedAt });
            e.HasOne(x => x.AiAlert)
                .WithMany(x => x.EscalationHistory)
                .HasForeignKey(x => x.AiAlertId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("AppUsers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.Role).HasMaxLength(64).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(1024).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Meeting>(e =>
        {
            e.ToTable("Meetings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(64).IsRequired();
            e.Property(x => x.CurrentStage).HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedByName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Context).HasMaxLength(8000).IsRequired();
            e.Property(x => x.InvolvedAreasCsv).HasMaxLength(1024).IsRequired();
            e.Property(x => x.AiSummary).HasMaxLength(8000).IsRequired();
            e.Property(x => x.CancellationReason).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.CreatedByUserId, x.Status });
            e.HasIndex(x => x.CreatedAt);
            e.HasMany(x => x.Participants).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Comments).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Problems).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Questions).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.AiAnalyses).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Decisions).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Actions).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.History).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingParticipant>(e =>
        {
            e.ToTable("MeetingParticipants");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            e.Property(x => x.UserEmail).HasMaxLength(256).IsRequired();
            e.Property(x => x.UserRole).HasMaxLength(64).IsRequired();
            e.Property(x => x.UserSector).HasMaxLength(128).IsRequired();
            e.Property(x => x.RoleInMeeting).HasMaxLength(64).IsRequired();
            e.Property(x => x.ParticipationStatus).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<MeetingComment>(e =>
        {
            e.ToTable("MeetingComments");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Stage).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.CreatedAt });
        });

        modelBuilder.Entity<MeetingProblem>(e =>
        {
            e.ToTable("MeetingProblems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sector).HasMaxLength(128).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(32).IsRequired();
            e.Property(x => x.Origin).HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedByName).HasMaxLength(256).IsRequired();
            e.Property(x => x.AiSuggestion).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.Sector });
            e.HasMany(x => x.Questions).WithOne(x => x.Problem).HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingQuestion>(e =>
        {
            e.ToTable("MeetingQuestions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Question).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ResponsibleName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Sector).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.Status });
            e.HasOne(x => x.Answer).WithOne(x => x.Question).HasForeignKey<MeetingAnswer>(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingAnswer>(e =>
        {
            e.ToTable("MeetingAnswers");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Sector).HasMaxLength(128).IsRequired();
            e.Property(x => x.Answer).HasMaxLength(8000).IsRequired();
        });

        modelBuilder.Entity<MeetingHistory>(e =>
        {
            e.ToTable("MeetingHistories");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            e.Property(x => x.DataBefore).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.DataAfter).HasColumnType("jsonb").IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.CreatedAt });
            e.HasIndex(x => x.EventType);
        });

        modelBuilder.Entity<MeetingAiAnalysis>(e =>
        {
            e.ToTable("MeetingAiAnalyses");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProblemDescription).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ProposedSolution).HasMaxLength(4000).IsRequired();
            e.Property(x => x.PositivePoints).HasMaxLength(4000).IsRequired();
            e.Property(x => x.NegativePoints).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Risks).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ExpectedImpact).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Recommendation).HasMaxLength(4000).IsRequired();
            e.Property(x => x.AlternativeSolution).HasMaxLength(4000).IsRequired();
            e.Property(x => x.SuggestedDecision).HasMaxLength(4000).IsRequired();
            e.Property(x => x.RelatedPendencies).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.ProblemId });
        });

        modelBuilder.Entity<MeetingDecision>(e =>
        {
            e.ToTable("MeetingDecisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProblemDescription).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ChosenSolution).HasMaxLength(8000).IsRequired();
            e.Property(x => x.SolutionOrigin).HasMaxLength(64).IsRequired();
            e.Property(x => x.Justification).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ResponsibleName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Sector).HasMaxLength(128).IsRequired();
            e.Property(x => x.Priority).HasMaxLength(32).IsRequired();
            e.Property(x => x.TrackingMetric).HasMaxLength(256).IsRequired();
            e.Property(x => x.AcceptedRisk).HasMaxLength(2000).IsRequired();
            e.Property(x => x.NextSteps).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ClosedPendencies).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.ProblemId });
        });

        modelBuilder.Entity<MeetingAction>(e =>
        {
            e.ToTable("MeetingActions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ResponsibleName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Sector).HasMaxLength(128).IsRequired();
            e.Property(x => x.Priority).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.CompletionEvidence).HasMaxLength(8000).IsRequired();
            e.Property(x => x.Comments).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.ResponsibleUserId, x.Status });
            e.HasIndex(x => new { x.Status, x.DeadlineAt });
            e.HasOne(x => x.Decision).WithMany().HasForeignKey(x => x.DecisionId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CriticalPending>(e =>
        {
            e.ToTable("CriticalPendencies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Origin).HasMaxLength(64).IsRequired();
            e.Property(x => x.Sector).HasMaxLength(128).IsRequired();
            e.Property(x => x.ResponsibleName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Priority).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(64).IsRequired();
            e.Property(x => x.NotificationHistoryJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.EscalationHistoryJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.AiSuggestion).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.Status, x.Priority });
            e.HasIndex(x => x.DeadlineAt);
            e.HasIndex(x => x.ResponsibleUserId);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Type).HasMaxLength(64).IsRequired();
            e.Property(x => x.Priority).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.RelatedLink).HasMaxLength(1024).IsRequired();
            e.Property(x => x.RelatedEntity).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });
    }
}
