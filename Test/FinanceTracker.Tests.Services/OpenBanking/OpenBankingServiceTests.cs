using FinanceTracker.Data;
using FinanceTracker.Models.External;
using FinanceTracker.Models.Request.OpenBanking;
using FinanceTracker.Services.External.OpenBanking;
using FinanceTracker.Services.OpenBanking;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Services.OpenBanking;

public class OpenBankingServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_ReturnsProvidersSortedByDisplayName()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"openbanking-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var api = Substitute.For<IOpenBankingApiService>();
        api.GetAvailableProvidersAsync(_cancellationTokenSource.Token)
            .Returns(new[]
            {
                new ExternalOpenBankingProvider { DisplayName = "Zeta" },
                new ExternalOpenBankingProvider { DisplayName = "Alpha" }
            }.ToAsyncEnumerable());

        var subject = new OpenBankingService(BuildUser(PrimaryUserId), factory, api,
            NullLogger<OpenBankingService>.Instance);

        List<string?> names = [];
        await foreach (var provider in subject.GetOpenBankingProvidersForClientAsync(_cancellationTokenSource.Token))
        {
            names.Add(provider.DisplayName);
        }

        await Assert.That(names).IsEquivalentTo(["Alpha", "Zeta"]);
    }

    [Test]
    public async Task BuildAuthUrl_DelegatesToExternalApiService()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"openbanking-url-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var api = Substitute.For<IOpenBankingApiService>();
        api.BuildAuthUrl(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<string>>()).Returns("https://example.test/auth");

        var subject = new OpenBankingService(BuildUser(PrimaryUserId), factory, api,
            NullLogger<OpenBankingService>.Instance);

        var url = subject.BuildAuthUrl(new GetProviderSetupUrlRequestModel
        {
            ProviderIds = ["provider-1"],
            Scopes = ["accounts", "transactions"]
        });

        await Assert.That(url).IsEqualTo("https://example.test/auth");
    }

    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_WhenExternalReturnsNoProviders_ReturnsEmpty()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"openbanking-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var api = Substitute.For<IOpenBankingApiService>();
        api.GetAvailableProvidersAsync(_cancellationTokenSource.Token)
            .Returns(Array.Empty<ExternalOpenBankingProvider>().ToAsyncEnumerable());

        var subject = new OpenBankingService(BuildUser(PrimaryUserId), factory, api,
            NullLogger<OpenBankingService>.Instance);

        var names = new List<string?>();
        await foreach (var provider in subject.GetOpenBankingProvidersForClientAsync(_cancellationTokenSource.Token))
        {
            names.Add(provider.DisplayName);
        }

        await Assert.That(names.Count).IsEqualTo(0);
    }
}
