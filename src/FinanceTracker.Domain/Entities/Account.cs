using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Aggregate root representing a financial account (e.g., checking, savings, credit card, investment).
/// </summary>
public class Account
{
    // Parameterless constructor for EF Core
    private Account()
    {
        Name = string.Empty;
        CurrencyCode = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public Account(
        Guid id,
        Guid householdId,
        string name,
        AccountType accountType,
        OwnershipType ownershipType,
        string? ownerUserId,
        string currencyCode,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Account Id cannot be empty.", nameof(id));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household Id cannot be empty.", nameof(householdId));
        }

        ValidateName(name);
        ValidateCurrency(currencyCode);
        ValidateCreatedByUser(createdByUserId);

        string? validatedOwner = ValidateOwnership(ownershipType, ownerUserId);

        Id = id;
        HouseholdId = householdId;
        Name = name.Trim();
        AccountType = accountType;
        OwnershipType = ownershipType;
        OwnerUserId = validatedOwner;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        CreatedByUserId = createdByUserId.Trim();
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; }
    public AccountType AccountType { get; private set; }
    public OwnershipType OwnershipType { get; private set; }
    public string? OwnerUserId { get; private set; }
    public string CurrencyCode { get; private set; }
    public bool IsActive { get; private set; }
    public string CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation property
    public Household Household { get; private set; } = null!;

    public void Update(
        string newName,
        AccountType newAccountType,
        OwnershipType newOwnershipType,
        string? newOwnerUserId,
        DateTimeOffset updatedAt)
    {
        ValidateName(newName);
        string? validatedOwner = ValidateOwnership(newOwnershipType, newOwnerUserId);

        Name = newName.Trim();
        AccountType = newAccountType;
        OwnershipType = newOwnershipType;
        OwnerUserId = validatedOwner;
        UpdatedAt = updatedAt;
    }

    public void Deactivate(DateTimeOffset updatedAt)
    {
        IsActive = false;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        IsActive = true;
        UpdatedAt = updatedAt;
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmed = name.Trim();
        if (trimmed.Length > 200)
        {
            throw new ArgumentException("Account name cannot exceed 200 characters.", nameof(name));
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

    private static string? ValidateOwnership(OwnershipType ownershipType, string? ownerUserId)
    {
        if (ownershipType == OwnershipType.Individual)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                throw new ArgumentException(
                    "OwnerUserId is required when OwnershipType is Individual.",
                    nameof(ownerUserId));
            }

            string trimmedOwner = ownerUserId.Trim();
            if (trimmedOwner.Length > 450)
            {
                throw new ArgumentException("OwnerUserId cannot exceed 450 characters.", nameof(ownerUserId));
            }

            return trimmedOwner;
        }

        // Joint ownership does not belong to a single individual user
        return null;
    }
}
