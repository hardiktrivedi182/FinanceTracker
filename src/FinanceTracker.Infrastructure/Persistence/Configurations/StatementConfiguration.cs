using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public class StatementConfiguration : IEntityTypeConfiguration<Statement>
{
    public void Configure(EntityTypeBuilder<Statement> builder)
    {
        builder.ToTable("statements");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.HouseholdId)
            .HasColumnName("household_id")
            .IsRequired();

        builder.Property(s => s.UploaderUserId)
            .HasColumnName("uploader_user_id")
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(s => s.OriginalFileName)
            .HasColumnName("original_filename")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.Format)
            .HasColumnName("format")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .IsRequired();

        builder.Property(s => s.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.ParsedTransactionCount)
            .HasColumnName("parsed_transaction_count")
            .IsRequired(false);

        builder.Property(s => s.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(s => s.ParseSummaryJson)
            .HasColumnName("parse_summary_json")
            .IsRequired(false);

        builder.HasIndex(s => new { s.HouseholdId, s.UploadedAt })
            .HasDatabaseName("ix_statements_household_uploaded_at");

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(s => s.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
