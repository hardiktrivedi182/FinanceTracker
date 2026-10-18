using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.HouseholdId)
            .HasColumnName("household_id")
            .IsRequired();

        builder.Property(t => t.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(t => t.StatementId)
            .HasColumnName("statement_id")
            .IsRequired(false);

        builder.Property(t => t.ImportBatchId)
            .HasColumnName("import_batch_id")
            .IsRequired(false);

        builder.Property(t => t.SourceRowNumber)
            .HasColumnName("source_row_number")
            .IsRequired(false);

        builder.Property(t => t.TransactionDate)
            .HasColumnName("transaction_date")
            .IsRequired();

        builder.Property(t => t.OriginalTransactionDate)
            .HasColumnName("original_transaction_date")
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(t => t.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.SignedAmount)
            .HasColumnName("signed_amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(t => t.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(t => t.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(t => new { t.HouseholdId, t.AccountId, t.TransactionDate })
            .HasDatabaseName("ix_transactions_household_account_date");

        builder.HasIndex(t => new { t.AccountId, t.Fingerprint })
            .HasDatabaseName("ix_transactions_account_fingerprint");

        builder.HasIndex(t => t.ImportBatchId)
            .HasDatabaseName("ix_transactions_import_batch_id");

        builder.HasIndex(t => t.StatementId)
            .HasDatabaseName("ix_transactions_statement_id");

        builder.HasOne(t => t.Household)
            .WithMany()
            .HasForeignKey(t => t.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Statement)
            .WithMany()
            .HasForeignKey(t => t.StatementId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.ImportBatch)
            .WithMany()
            .HasForeignKey(t => t.ImportBatchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
