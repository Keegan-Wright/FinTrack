using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Integration.OpenBanking;

namespace FinanceTracker.Tests.Integration.Api;

public class OpenBankingApiTests : IntegrationTestBase
{
    [Test]
    public async Task Providers_AreListedByName()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        List<OpenBankingProvider> providers = await ApiFor(user).Get<IOpenBankingService>()
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(providers.Select(x => x.DisplayName)).IsEquivalentTo(["Fake Bank", "Other Bank"]);
            await Assert.That(providers[0].Scopes).IsEquivalentTo(["accounts", "balance", "transactions"]);
        }
    }

    [Test]
    public async Task BuildAuthUrl_IsBuiltOnTheServer()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        string url = await ApiFor(user).Get<IOpenBankingService>().BuildAuthUrlAsync(new ProviderSetupUrl
        {
            ProviderIds = ["ob-fake-bank"],
            Scopes = ["accounts"]
        }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(url).StartsWith(App.TrueLayer.AuthUrl.ToString());
            await Assert.That(url).Contains($"client_id={FakeTrueLayer.ClientId}");
            await Assert.That(url).Contains("ob-fake-bank");
        }
    }

    [Test]
    public async Task AddVendor_WithACodeTheProviderRejects_Fails()
    {
        // Arrange, the fake provider has no token stubbed for the code
        SignedInUser user = await RegisterUserAsync();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => ApiFor(user).Get<IOpenBankingService>()
            .AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = UniqueName("code") }, CancellationToken));
    }
}
