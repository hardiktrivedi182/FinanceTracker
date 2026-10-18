using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public class ImportStagingRowConfiguration : IEntityTypeConfiguration<ImportStagingRow>
{
    public void Configure(EntityTypeBuilder<ImportStagingRow> builder)
    {
        builder.ToTable("import_staging_rows");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.HouseholdId)
            .HasColumnName("household_id")
            .IsRequired();

        builder.Property(r => r.ImportBatchId)
            .HasColumnName("import_batch_id")
            .IsRequired();

        builder.Property(r => r.SourceRowNumber)
            .HasColumnName("source_row_number")
            .IsRequired();

        builder.Property(r => r.RawData)
            .HasColumnName("raw_data")
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(r => r.TransactionDate)
            .HasColumnName("transaction_date")
            .IsRequired(false);

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 4)
            .IsRequired(false);

        builder.Property(r => r.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(r => r.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsRequired(false);

        builder.Property(r => r.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.ValidationStatus)
            .HasColumnName("validation_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ValidationErrors)
            .HasColumnName("validation_errors")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(r => r.DuplicateStatus)
            .HasColumnName("duplicate_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.DuplicateReason)
            .HasColumnName("duplicate_reason")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(r => r.MatchedTransactionId)
            .HasColumnName("matched_transaction_id")
            .IsRequired(false);

        builder.Property(r => r.IsSelected)
            .HasColumnName("is_selected")
            .IsRequired();

        builder.Property(r => r.Resolution)
            .HasColumnName("resolution")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ResolutionNotes)
            .HasColumnName("resolution_notes")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(r => r.ResolvedTransactionDate)
            .HasColumnName("resolved_transaction_date")
            .IsRequired(false);

        builder.Property(r => r.ResolvedDescription)
            .HasColumnName("resolved_description")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(r => r.ResolvedAmount)
            .HasColumnName("resolved_amount")
            .HasPrecision(19, 4)
            .IsRequired(false);

        builder.Property(r => r.ResolvedType)
            .HasColumnName("resolved_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(r => r.CommittedTransactionId)
            .HasColumnName("committed_transaction_id")
            .IsRequired(false);

        builder.HasIndex(r => new { r.ImportBatchId, r.SourceRowNumber })
            .HasDatabaseName("ix_import_staging_rows_batch_row_number");

        builder.HasIndex(r => new { r.HouseholdId, r.ImportBatchId })
            .HasDatabaseName("ix_import_staging_rows_household_batch");

        builder.HasIndex(r => r.Fingerprint)
            .HasDatabaseName("ix_import_staging_rows_fingerprint");
    }
}
