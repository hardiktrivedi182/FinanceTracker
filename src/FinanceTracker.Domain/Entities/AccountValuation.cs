using FinanceTracker.Domain.Enums;
using FinanceTracker.Domain.ValueObjects;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Represents a dated valuation snapshot for an account or asset holding.
/// Useful for capturing periodic statement balances, passbook entries (e.g. EPF/PPF), or market valuations.
/// </summary>
public class AccountValuation
{
    // Parameterless constructor for EF Core
    private AccountValuation()
    {
        CurrencyCode = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public AccountValuation(
        Guid id,
        Guid accountId,
        DateOnly valuationDate,
        decimal amount,
        string currencyCode,
        ValuationSourceType sourceType,
        string? sourceReference,
        string? notes,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Valuation Id cannot be empty.", nameof(id));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account Id cannot be empty.", nameof(accountId));
        }

        ValidateCurrency(currencyCode);
        ValidateCreatedByUser(createdByUserId);

        Id = id;
        AccountId = accountId;
        ValuationDate = valuationDate;
        Amount = amount;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        SourceType = sourceType;
        SourceReference = sourceReference?.Trim();
        Notes = notes?.Trim();
        CreatedByUserId = createdByUserId.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public DateOnly ValuationDate { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; }
    public ValuationSourceType SourceType { get; private set; }
    public string? SourceReference { get; private set; }
    public string? Notes { get; private set; }
    public string CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? UpdatedByUserId { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(
        decimal newAmount,
        DateOnly newDate,
        ValuationSourceType newSourceType,
        string? newSourceReference,
        string? newNotes,
        string modifiedByUserId,
        DateTimeOffset modifiedAt)
    {
        ValidateCreatedByUser(modifiedByUserId);

        Amount = newAmount;
        ValuationDate = newDate;
        SourceType = newSourceType;
        SourceReference = newSourceReference?.Trim();
        Notes = newNotes?.Trim();
        UpdatedByUserId = modifiedByUserId.Trim();
        UpdatedAt = modifiedAt;
    }

    // Navigation property
    public Account Account { get; private set; } = null!;

    public Money AsMoney() => new(Amount, CurrencyCode);

    private static void ValidateCurrency(string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        string trimmed = currencyCode.Trim();
        if (trimmed.Length != 3 || !trimmed.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Currency code must be a 3-letter ISO 4217 ASCII code.", nameof(currencyCode));
        }
    }

    private static void ValidateCreatedByUser(string createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByUserId);
        string trimmed = createdByUserId.Trim();
        if (trimmed.Length > 450)
        {
            throw new ArgumentException("CreatedByUserId cannot exceed 450 characters.", nameof(createdByUserId));
        }
    }
}
