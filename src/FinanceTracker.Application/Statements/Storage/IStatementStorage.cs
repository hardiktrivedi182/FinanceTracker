namespace FinanceTracker.Application.Statements.Storage;

/// <summary>
/// Abstraction for storing uploaded statement binary files.
/// Implemented by LocalStorageStatementStorage (development/test) and AzureBlobStatementStorage (Azure).
/// </summary>
public interface IStatementStorage
{
    /// <summary>
    /// Persists statement stream content and returns an internal, safe storage key.
    /// Never returns a public or credentials-containing URL.
    /// </summary>
    Task<string> SaveAsync(Guid householdId, Guid statementId, Stream content, string extension, CancellationToken ct = default);

    /// <summary>
    /// Opens a read stream for the specified storage key.
    /// </summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes stored statement file (used for rollbacks, cleanups, or deletions).
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Checks if a file exists for the given storage key.
    /// </summary>
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
}
