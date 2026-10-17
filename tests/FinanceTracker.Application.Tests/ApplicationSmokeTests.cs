using Xunit;

namespace FinanceTracker.Application.Tests;

public class ApplicationSmokeTests
{
    [Fact]
    public void ApplicationAssembly_CanBeLoaded()
    {
        // Verifies the Application assembly loads and its marker is accessible.
        var marker = typeof(ApplicationAssemblyMarker);
        Assert.NotNull(marker);
    }
}
