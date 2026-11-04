using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Connections;

public class ProviderSetupPageTests : ComponentTestBase
{
    private readonly IOpenBankingService _openBankingService = Substitute.For<IOpenBankingService>();
    private readonly ApplicationState _applicationState = new();

    public ProviderSetupPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_openBankingService);
        _openBankingService.GetOpenBankingProvidersForClientAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<OpenBankingProvider>());
    }

    [Test]
    public async Task Synced_ShowsAccessThatHasEnded()
    {
        // Arrange
        using CultureScope culture = new();
        ConsentState state = ConsentState.Active;
        _openBankingService.GetConnectionsAsync(Arg.Any<CancellationToken>()).Returns(_ => new[]
        {
            new BankConnectionSummary
            {
                ConnectionId = Guid.NewGuid(),
                Name = "Bank",
                Logo = [],
                ConsentExpiresAt = new DateTime(2099, 1, 31, 12, 0, 0, DateTimeKind.Utc),
                ConsentState = state
            }
        }.ToAsyncEnumerable());
        IRenderedComponent<ProviderSetup> cut = Render<ProviderSetup>(p => p.AddCascadingValue(_applicationState));
        state = ConsentState.Lapsed;

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Access ended 31 Jan 2099, renew to sync again");
            await Assert.That(_applicationState.ConnectionsToRenew).IsEqualTo(1);
        }
    }
}
