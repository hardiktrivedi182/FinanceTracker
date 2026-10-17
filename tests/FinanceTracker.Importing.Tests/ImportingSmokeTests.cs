using Xunit;

namespace FinanceTracker.Importing.Tests;

public class ImportingSmokeTests
{
    [Fact]
    public void ImportingAssembly_CanBeLoaded()
    {
        var marker = typeof(ImportingAssemblyMarker);
        Assert.NotNull(marker);
    }
}
