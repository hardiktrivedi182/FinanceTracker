using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batches");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasColumnName("id");

        builder.Property(b => b.HouseholdId)
            .HasColumnName("household_id")
            .IsRequired();

        builder.Property(b => b.StatementId)
            .HasColumnName("statement_id")
            .IsRequired();

        builder.Property(b => b.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(b => b.UploadedByUserId)
            .HasColumnName("uploaded_by_user_id")
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.TotalRowCount)
            .HasColumnName("total_row_count")
            .IsRequired();

        builder.Property(b => b.ReadyCount)
            .HasColumnName("ready_count")
            .IsRequired();

        builder.Property(b => b.DuplicateCount)
            .HasColumnName("duplicate_count")
            .IsRequired();

        builder.Property(b => b.ErrorCount)
            .HasColumnName("error_count")
            .IsRequired();

        builder.Property(b => b.SelectedRowCount)
            .HasColumnName("selected_row_count")
            .IsRequired();

        builder.Property(b => b.CommittedCount)
            .HasColumnName("committed_count")
            .IsRequired(false);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.CommittedAt)
            .HasColumnName("committed_at")
            .IsRequired(false);

        builder.Property(b => b.CommittedByUserId)
            .HasColumnName("committed_by_user_id")
            .HasMaxLength(450)
            .IsRequired(false);

        builder.Property(b => b.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        builder.HasIndex(b => new { b.HouseholdId, b.CreatedAt })
            .HasDatabaseName("ix_import_batches_household_created_at");

        builder.HasIndex(b => b.StatementId)
            .HasDatabaseName("ix_import_batches_statement_id");

        builder.HasIndex(b => b.AccountId)
            .HasDatabaseName("ix_import_batches_account_id");

        builder.HasOne(b => b.Household)
            .WithMany()
            .HasForeignKey(b => b.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Statement)
            .WithMany()
            .HasForeignKey(b => b.StatementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Account)
            .WithMany()
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Rows)
            .WithOne(r => r.ImportBatch)
            .HasForeignKey(r => r.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
