using System.Text;
using FinanceTracker.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceTracker.Infrastructure.Tests.Storage;

public class LocalStorageStatementStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalStorageStatementStorage _storage;

    public LocalStorageStatementStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FinanceTrackerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new LocalStorageOptions { RootPath = _tempDir });
        _storage = new LocalStorageStatementStorage(options);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveAsync_StoresContentAndReturnsStorageKey()
    {
        var householdId = Guid.NewGuid();
        var statementId = Guid.NewGuid();
        var content = "Date,Description,Amount\n2026-01-01,Test,10.00";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var storageKey = await _storage.SaveAsync(householdId, statementId, stream, ".csv");

        storageKey.Should().NotBeNullOrWhiteSpace();
        storageKey.Should().Contain(householdId.ToString());
        storageKey.Should().Contain(statementId.ToString());

        var exists = await _storage.ExistsAsync(storageKey);
        exists.Should().BeTrue();

        await using var readStream = await _storage.OpenReadAsync(storageKey);
        using var reader = new StreamReader(readStream, Encoding.UTF8);
        var readContent = await reader.ReadToEndAsync();

        readContent.Should().Be(content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFileFromStorage()
    {
        var householdId = Guid.NewGuid();
        var statementId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        var key = await _storage.SaveAsync(householdId, statementId, stream, ".csv");
        (await _storage.ExistsAsync(key)).Should().BeTrue();

        await _storage.DeleteAsync(key);

        (await _storage.ExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task OpenReadAsync_WhenPathTraversalAttempted_ThrowsInvalidOperationException()
    {
        var maliciousKey = "../../../../Windows/System32/drivers/etc/hosts";

        var act = async () => await _storage.OpenReadAsync(maliciousKey);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*path traversal*");
    }
}
