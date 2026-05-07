using Microsoft.EntityFrameworkCore;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Data;

public class NexusDbContext : DbContext
{
    public NexusDbContext(DbContextOptions<NexusDbContext> options) : base(options)
    {
    }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<InsuranceTransaction> InsuranceTransactions => Set<InsuranceTransaction>();
    public DbSet<SanitizationLog> SanitizationLogs => Set<SanitizationLog>();
    public DbSet<CarrierMapping> CarrierMappings => Set<CarrierMapping>();
    public DbSet<CanonicalField> CanonicalFields => Set<CanonicalField>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasMany(x => x.DirectReports)
                .WithOne(x => x.ReportsTo)
                .HasForeignKey(x => x.ReportsToId);
        });

        modelBuilder.Entity<Batch>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceName).HasMaxLength(200).IsRequired();
            entity.HasMany(x => x.Transactions)
                .WithOne(x => x.Batch)
                .HasForeignKey(x => x.BatchId);
        });

        modelBuilder.Entity<InsuranceTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PolicyNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CarrierCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GrossPremium).HasColumnType("decimal(18,2)");
            entity.Property(x => x.NetCommission).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ConfidenceScore).HasColumnType("decimal(5,4)");
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.PIIMasked).HasDefaultValue(false);
            entity.HasIndex(x => new { x.BatchId, x.ExternalId }).IsUnique(false);
        });

        modelBuilder.Entity<SanitizationLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.HasOne(x => x.Transaction)
                .WithMany(x => x.SanitizationLogs)
                .HasForeignKey(x => x.TransactionId);
        });

        modelBuilder.Entity<CarrierMapping>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CarrierCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SourceField).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TargetField).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TransformRule).HasMaxLength(500);
            entity.Property(x => x.IsRequired).HasDefaultValue(false);
            entity.HasIndex(x => new { x.CarrierCode, x.SourceField }).IsUnique();
        });

        modelBuilder.Entity<CanonicalField>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FieldName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DataType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Examples).HasMaxLength(2000);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.HasIndex(x => x.FieldName).IsUnique();
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.SortOrder);
        });
    }
}
