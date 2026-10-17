using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public class AccountValuationConfiguration : IEntityTypeConfiguration<AccountValuation>
{
    public void Configure(EntityTypeBuilder<AccountValuation> builder)
    {
        builder.ToTable("account_valuations");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnName("id");

        builder.Property(v => v.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(v => v.ValuationDate)
            .HasColumnName("valuation_date")
            .IsRequired();

        builder.Property(v => v.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(v => v.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(v => v.SourceType)
            .HasColumnName("source_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.SourceReference)
            .HasColumnName("source_reference")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(v => v.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(v => v.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(v => v.UpdatedByUserId)
            .HasColumnName("updated_by_user_id")
            .HasMaxLength(450)
            .IsRequired(false);

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        builder.HasIndex(v => new { v.AccountId, v.ValuationDate });
    }
}
