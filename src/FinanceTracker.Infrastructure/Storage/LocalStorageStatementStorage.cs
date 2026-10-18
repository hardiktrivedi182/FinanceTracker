using FinanceTracker.Application.Statements.Storage;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Storage;

public class LocalStorageOptions
{
    public const string SectionName = "StatementStorage:Local";
    public string RootPath { get; set; } = "App_Data/statements";
}

/// <summary>
/// Local filesystem implementation of IStatementStorage for development, testing, and offline environments.
/// Enforces directory containment to prevent path traversal attacks.
/// </summary>
public class LocalStorageStatementStorage : IStatementStorage
{
    private readonly string _basePath;

    public LocalStorageStatementStorage(IOptions<LocalStorageOptions> options)
    {
        _basePath = Path.GetFullPath(options.Value.RootPath);
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> SaveAsync(Guid householdId, Guid statementId, Stream content, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Sanitize extension
        var safeExt = extension.StartsWith('.') ? extension : "." + extension;
        if (safeExt.Contains('/') || safeExt.Contains('\\'))
        {
            safeExt = ".csv";
        }

        var relativePath = Path.Combine(householdId.ToString(), $"{statementId}{safeExt}");
        var fullPath = GetSafeFullPath(relativePath);

        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await content.CopyToAsync(fileStream, ct);

        // Return forward-slash relative storage key
        return relativePath.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = GetSafeFullPath(storageKey);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Statement file not found for storage key '{storageKey}'.");
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = GetSafeFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = GetSafeFullPath(storageKey);
        return Task.FromResult(File.Exists(fullPath));
    }

    private string GetSafeFullPath(string storageKey)
    {
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var combined = Path.Combine(_basePath, normalized);
        var fullPath = Path.GetFullPath(combined);

        // Defense-in-depth: Ensure the resolved path resides strictly within _basePath
        if (!fullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Access denied: path traversal attempt detected.");
        }

        return fullPath;
    }
}
