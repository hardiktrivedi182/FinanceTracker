using FinanceTracker.Domain.Enums;
using FinanceTracker.Domain.ValueObjects;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Domain aggregate entity representing an immutable ledger transaction.
/// Transactions are created only via explicit user-confirmed import or authorized manual creation.
/// Amount-sign convention:
///   - Amount is strictly non-negative (>= 0).
///   - Type represents direction (Debit or Credit).
///   - SignedAmount convention: Credit is positive (+Amount), Debit is negative (-Amount).
/// </summary>
public class Transaction
{
    // Parameterless constructor for EF Core
    private Transaction()
    {
        Description = string.Empty;
        CurrencyCode = string.Empty;
        Fingerprint = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public Transaction(
        Guid id,
        Guid householdId,
        Guid accountId,
        Guid? statementId,
        Guid? importBatchId,
        int? sourceRowNumber,
        DateOnly transactionDate,
        DateOnly? originalTransactionDate,
        string description,
        decimal amount,
        TransactionType type,
        string currencyCode,
        string fingerprint,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Transaction Id cannot be empty.", nameof(id));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household Id cannot be empty.", nameof(householdId));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account Id cannot be empty.", nameof(accountId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Transaction amount must be strictly positive.", nameof(amount));
        }

        ValidateDescription(description);
        ValidateCurrency(currencyCode);
        ValidateCreatedByUser(createdByUserId);

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new ArgumentException("Fingerprint cannot be empty.", nameof(fingerprint));
        }

        Id = id;
        HouseholdId = householdId;
        AccountId = accountId;
        StatementId = statementId;
        ImportBatchId = importBatchId;
        SourceRowNumber = sourceRowNumber;
        TransactionDate = transactionDate;
        OriginalTransactionDate = originalTransactionDate ?? transactionDate;
        Description = description.Trim();
        Amount = amount;
        Type = type;
        SignedAmount = type == TransactionType.Credit ? amount : -amount;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Fingerprint = fingerprint.Trim().ToLowerInvariant();
        CreatedByUserId = createdByUserId.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid? StatementId { get; private set; }
    public Guid? ImportBatchId { get; private set; }
    public int? SourceRowNumber { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public DateOnly OriginalTransactionDate { get; private set; }
    public string Description { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal SignedAmount { get; private set; }
    public string CurrencyCode { get; private set; }
    public string Fingerprint { get; private set; }
    public string CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation properties
    public Household Household { get; private set; } = null!;
    public Account Account { get; private set; } = null!;
    public Statement? Statement { get; private set; }
    public ImportBatch? ImportBatch { get; private set; }

    public Money AsMoney() => new(Amount, CurrencyCode);

    private static void ValidateDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        string trimmed = description.Trim();
        if (trimmed.Length > 500)
        {
            throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));
        }
    }

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
