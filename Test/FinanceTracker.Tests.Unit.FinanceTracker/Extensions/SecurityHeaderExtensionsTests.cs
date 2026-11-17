using FinanceTracker.Extensions;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class SecurityHeaderExtensionsTests
{
    [Test]
    public async Task AddSecurityHeaders_KeepsBrowsersOnHttpsForAYear_NotForOtherAppsOnSubdomains()
    {
        // Arrange
        await using ServiceProvider provider = new ServiceCollection().AddSecurityHeaders().BuildServiceProvider();

        // Act
        HstsOptions options = provider.GetRequiredService<IOptions<HstsOptions>>().Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.MaxAge).IsEqualTo(TimeSpan.FromDays(365));
            await Assert.That(options.IncludeSubDomains).IsFalse();
            await Assert.That(options.Preload).IsFalse();
        }
    }
}
