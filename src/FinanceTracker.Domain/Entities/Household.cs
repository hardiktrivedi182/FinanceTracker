namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Top-level boundary representing a single household unit.
/// </summary>
public class Household
{
    private readonly List<HouseholdMember> _members = [];
    private readonly List<Account> _accounts = [];

    // Parameterless constructor for EF Core
    private Household()
    {
        Name = string.Empty;
    }

    public Household(Guid id, string name, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Household Id cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmedName = name.Trim();
        if (trimmedName.Length > 200)
        {
            throw new ArgumentException("Household name cannot exceed 200 characters.", nameof(name));
        }

        Id = id;
        Name = trimmedName;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<HouseholdMember> Members => _members.AsReadOnly();
    public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

    public void UpdateName(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        string trimmed = newName.Trim();
        if (trimmed.Length > 200)
        {
            throw new ArgumentException("Household name cannot exceed 200 characters.", nameof(newName));
        }

        Name = trimmed;
    }
}
