using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.OpenBanking;

public class OpenBankingServiceTests : OpenBankingTestBase
{
    private readonly IOpenBankingSyncScheduler _openBankingSyncScheduler = Substitute.For<IOpenBankingSyncScheduler>();

    private readonly HybridCache _hybridCache = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider()
        .GetRequiredService<HybridCache>();

    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_ReturnsProvidersSortedByDisplayName()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.GetAvailableProvidersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExternalOpenBankingProvider { DisplayName = "Zeta" },
            new ExternalOpenBankingProvider { DisplayName = "Alpha" },
            new ExternalOpenBankingProvider { DisplayName = "Mid" }
        }.ToAsyncEnumerable());

        OpenBankingService subject = BuildSubject();

        // Act
        List<OpenBankingProvider> providers = await subject
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(string.Join(",", providers.Select(x => x.DisplayName))).IsEqualTo("Alpha,Mid,Zeta");
    }

    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_AsksTheProviderOnceWhileCached()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.GetAvailableProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new[] { new ExternalOpenBankingProvider { DisplayName = "Bank" } }.ToAsyncEnumerable());

        // Act
        List<OpenBankingProvider> first = await BuildSubject()
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken);
        List<OpenBankingProvider> second = await BuildSubject()
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(second.Select(x => x.DisplayName)).IsEquivalentTo(first.Select(x => x.DisplayName));
            OpenBankingApi.Received(1).GetAvailableProvidersAsync(Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_WhenTheProviderFails_DoesNotCacheTheFailure()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.GetAvailableProvidersAsync(Arg.Any<CancellationToken>()).Returns(
            _ => throw new HttpRequestException("provider down"),
            _ => new[] { new ExternalOpenBankingProvider { DisplayName = "Bank" } }.ToAsyncEnumerable());

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(() => BuildSubject()
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken).AsTask());
        List<OpenBankingProvider> retried = await BuildSubject()
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(retried.Select(x => x.DisplayName)).IsEquivalentTo(["Bank"]);
    }

    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_MapsProviderDetailsAndScopes()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.GetAvailableProvidersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExternalOpenBankingProvider
            {
                ProviderId = "uk-ob-bank",
                DisplayName = "Bank",
                Country = "gb",
                LogoUrl = "https://example.com/bank.svg",
                Scopes = new[] { "accounts", "transactions" }.ToAsyncEnumerable()
            },
            new ExternalOpenBankingProvider { DisplayName = "No scopes" }
        }.ToAsyncEnumerable());

        OpenBankingService subject = BuildSubject();

        // Act
        List<OpenBankingProvider> providers = await subject
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        OpenBankingProvider bank = providers.Single(x => x.DisplayName == "Bank");
        using (Assert.Multiple())
        {
            await Assert.That(bank.ProviderId).IsEqualTo("uk-ob-bank");
            await Assert.That(bank.Country).IsEqualTo("gb");
            await Assert.That(bank.LogoUrl).IsEqualTo("https://example.com/bank.svg");
            await Assert.That(bank.Scopes).IsEquivalentTo(["accounts", "transactions"]);
            await Assert.That(providers.Single(x => x.DisplayName == "No scopes").Scopes).IsEmpty();
        }
    }

    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_WhenExternalReturnsNoProviders_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.GetAvailableProvidersAsync(CancellationToken)
            .Returns(Array.Empty<ExternalOpenBankingProvider>().ToAsyncEnumerable());

        OpenBankingService subject = BuildSubject();

        // Act
        List<OpenBankingProvider> providers = await subject
            .GetOpenBankingProvidersForClientAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(providers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildAuthUrlAsync_DelegatesProvidersAndScopesToExternalApiService()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.BuildAuthUrl(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<string>>())
            .Returns("https://example.test/auth");

        OpenBankingService subject = BuildSubject();

        // Act
        string url = await subject.BuildAuthUrlAsync(new ProviderSetupUrl
        {
            ProviderIds = ["provider-1"],
            Scopes = ["accounts", "transactions"]
        }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(url).IsEqualTo("https://example.test/auth");
            OpenBankingApi.Received(1).BuildAuthUrl(
                Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new[] { "provider-1" })),
                Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new[] { "accounts", "transactions" })));
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ExistingConnection_ReauthenticatesIssuesTokenAndQueuesASync()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(90))))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: "old-token"));

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate() }
                    .ToAsyncEnumerable()
            });

        OpenBankingService subject = BuildSubject();

        // Act
        bool result =
            await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection connection = await context.BankConnections.Include(x => x.AccessTokens)
            .SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(connection.Id).IsEqualTo(Scenario.ConnectionMappings["bank"]);
            await Assert.That(connection.AccessCode).IsEqualTo("new-code");
            await Assert.That(connection.ActiveToken!.Token).IsEqualTo(Token);
            await Assert.That(connection.AccessTokens.Single(x => x.Token == "old-token").IsRevoked).IsTrue();
            await _openBankingSyncScheduler.Received(1)
                .ScheduleUserSyncAsync(Scenario.UserMappings[Username], CancellationToken);
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_StoresTheProvidersConsentExpiry()
    {
        // Arrange
        DateTime expiresAt = new(2099, 1, 31, 12, 0, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(3))));

        ExternalOpenBankingAccountConnection external =
            new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate();
        external.ConsentExpiresAt = expiresAt;
        GivenCodeExchangeFor(external);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await Assert.That((await LoadConnectionAsync("bank")).ConsentExpiresAt).IsEqualTo(expiresAt);
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ProviderGivesNoExpiry_Assumes90Days()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(3))));

        ExternalOpenBankingAccountConnection external =
            new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate();
        external.ConsentExpiresAt = default;
        GivenCodeExchangeFor(external);

        OpenBankingService subject = BuildSubject();
        DateTime before = DateTime.UtcNow;

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await Assert.That((await LoadConnectionAsync("bank")).ConsentExpiresAt)
            .IsBetween(before.AddDays(90), DateTime.UtcNow.AddDays(90));
    }

    [Test]
    public async Task GetConnectionsAsync_ReportsEachConnectionsConsentState()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("active", Username, Named("Active Bank")))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("active", DateTime.UtcNow.AddDays(60)))
            .Do(s => s.BankConnectionScenario.WithConnection("soon", Username, Named("Soon Bank")))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("soon", DateTime.UtcNow.AddDays(5)))
            .Do(s => s.BankConnectionScenario.WithConnection("lapsed", Username, Named("Lapsed Bank")))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("lapsed", DateTime.UtcNow.AddDays(-1)))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername, Named("Their Bank")));

        OpenBankingService subject = BuildSubject();

        // Act
        List<BankConnectionSummary> connections = await subject.GetConnectionsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(connections.Select(x => (x.Name, x.ConsentState))).IsEquivalentTo([
            ("Active Bank", ConsentState.Active),
            ("Lapsed Bank", ConsentState.Lapsed),
            ("Soon Bank", ConsentState.RenewSoon)
        ]);
    }

    [Test]
    public async Task GetConnectionsAsync_SaysWhenStandingOrdersAndDirectDebitsWereLastFetched()
    {
        // Arrange, one connection with both fetched at different times and a later sync that left them out, one
        // never fetched
        DateTime connectedAt = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("fetched", Username, Named("Fetched Bank")))
            .Do(s => s.BankAccountScenario.WithAccount("current", "fetched"))
            .Do(s => s.BankAccountScenario.WithSyncLog("current", SyncType.StandingOrders, connectedAt))
            .Do(s => s.BankAccountScenario.WithSyncLog("current", SyncType.DirectDebits, connectedAt.AddMinutes(1)))
            .Do(s => s.BankAccountScenario.WithSyncLog("current", SyncType.Transactions, connectedAt.AddDays(5)))
            .Do(s => s.BankConnectionScenario.WithConnection("never", Username, Named("Never Bank")));

        OpenBankingService subject = BuildSubject();

        // Act
        List<BankConnectionSummary> connections = await subject.GetConnectionsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert, the later of the two, not the transactions sync
        await Assert.That(connections.Select(x => (x.Name, x.RecurringPaymentsUpdatedAt))).IsEquivalentTo([
            ("Fetched Bank", (DateTime?)connectedAt.AddMinutes(1)),
            ("Never Bank", (DateTime?)null)
        ]);
    }

    [Test]
    public async Task GetRenewalUrlAsync_UsesTheLatestRefreshTokenEvenOnceRevoked()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", DateTime.UtcNow.AddDays(-2),
                refreshToken: "older-refresh"))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", DateTime.UtcNow.AddDays(-1),
                refreshToken: "latest-refresh"))
            .Do(s => s.BankConnectionScenario.WithRevokedToken("bank", DateTime.UtcNow));

        OpenBankingApi.GetReauthenticationUrlAsync("latest-refresh", CancellationToken)
            .Returns("https://bank.example.test/reauth");

        OpenBankingService subject = BuildSubject();

        // Act
        string url = await subject.GetRenewalUrlAsync(Scenario.ConnectionMappings["bank"], CancellationToken);

        // Assert
        await Assert.That(url).IsEqualTo("https://bank.example.test/reauth");
    }

    [Test]
    public async Task GetRenewalUrlAsync_AnotherUsersConnection_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("theirs"));

        OpenBankingService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.GetRenewalUrlAsync(Scenario.ConnectionMappings["theirs"], CancellationToken));
        await OpenBankingApi.DidNotReceive()
            .GetReauthenticationUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_NewProvider_CreatesConnectionWithLogoAndTokenAndQueuesASync()
    {
        // Arrange
        byte[] logo = [137, 80, 78, 71];
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        ExternalOpenBankingAccountConnection external =
            new ExternalOpenBankingAccountConnectionFaker("ob-new").Generate();

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { external }.ToAsyncEnumerable()
            });
        OpenBankingApi.GetProviderLogoAsync(external.Provider!.LogoUri, CancellationToken).Returns(logo);

        OpenBankingService subject = BuildSubject();
        DateTime before = DateTime.UtcNow;

        // Act
        bool result =
            await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection connection = await context.BankConnections.Include(x => x.AccessTokens)
            .SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(connection.UserId).IsEqualTo(Scenario.UserMappings[Username]);
            await Assert.That(connection.ExternalProviderId).IsEqualTo("ob-new");
            await Assert.That(connection.Name).IsEqualTo(external.Provider.DisplayName);
            await Assert.That(connection.AccessCode).IsEqualTo("new-code");
            await Assert.That(connection.Logo).IsEquivalentTo(logo);
            await Assert.That(connection.Scopes).IsEquivalentTo(Shared.Fakers.BankConnectionFaker.Scopes);
            await Assert.That(connection.AccessTokens.Count).IsEqualTo(1);
            await Assert.That(connection.ActiveToken!.Token).IsEqualTo(Token);
            await Assert.That(connection.ActiveToken.RefreshToken).IsEqualTo("refresh");
            await Assert.That(connection.ActiveToken.IssuedAt).IsGreaterThanOrEqualTo(before);
            await _openBankingSyncScheduler.Received(1)
                .ScheduleUserSyncAsync(Scenario.UserMappings[Username], CancellationToken);
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_SeveralProvidersOnOneConsent_CreatesEachAndQueuesOneSync()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new ExternalOpenBankingAccountConnectionFaker().Generate(2).ToAsyncEnumerable()
            });
        OpenBankingApi.GetProviderLogoAsync(Arg.Any<string>(), CancellationToken).Returns([1]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await context.BankConnections.CountAsync(CancellationToken)).IsEqualTo(2);
            await _openBankingSyncScheduler.Received(1)
                .ScheduleUserSyncAsync(Scenario.UserMappings[Username], CancellationToken);
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_DoesNotSyncWhileConnecting()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { new ExternalOpenBankingAccountConnectionFaker().Generate() }.ToAsyncEnumerable()
            });
        OpenBankingApi.GetProviderLogoAsync(Arg.Any<string>(), CancellationToken).Returns([1]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await OpenBankingApi.DidNotReceiveWithAnyArgs().GetAllAccountsAsync(default!, default);
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ExistingConnection_DoesNotDownloadLogo()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(90))));

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate() }
                    .ToAsyncEnumerable()
            });

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await OpenBankingApi.DidNotReceiveWithAnyArgs().GetProviderLogoAsync(default!, default);
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_LogoDownloadFails_ThrowsAndSavesNothing()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { new ExternalOpenBankingAccountConnectionFaker().Generate() }.ToAsyncEnumerable()
            });
        OpenBankingApi.GetProviderLogoAsync(Arg.Any<string>(), CancellationToken)
            .ThrowsAsync(new HttpRequestException("404"));

        OpenBankingService subject = BuildSubject();

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await context.BankConnections.CountAsync(CancellationToken)).IsEqualTo(0);
            await _openBankingSyncScheduler.DidNotReceiveWithAnyArgs().ScheduleUserSyncAsync(default, default);
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ExistingConnection_PersistsNewScopes()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(90))));

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate() }
                    .ToAsyncEnumerable()
            });

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        BankConnection connection = await LoadConnectionAsync("bank");
        await Assert.That(connection.Scopes).IsEquivalentTo(Shared.Fakers.BankConnectionFaker.Scopes);
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ProviderReturnsNoConnections_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.ExchangeCodeForAccessTokenAsync("code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken)
            .Returns(new ExternalOpenBankingAccountConnectionResponse());

        OpenBankingService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "code" }, CancellationToken));
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ConnectionWithoutProvider_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        OpenBankingApi.ExchangeCodeForAccessTokenAsync("code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { new ExternalOpenBankingAccountConnection { Provider = null } }.ToAsyncEnumerable()
            });

        OpenBankingService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "code" }, CancellationToken));
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ProviderReportsNoScopes_CreatesConnectionWithoutScopes()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        ExternalOpenBankingAccountConnection external = new ExternalOpenBankingAccountConnectionFaker().Generate();
        external.Scopes = null;

        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { external }.ToAsyncEnumerable()
            });
        OpenBankingApi.GetProviderLogoAsync(Arg.Any<string>(), CancellationToken).Returns([1]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That((await context.BankConnections.SingleAsync(CancellationToken)).Scopes.Count).IsEqualTo(0);
    }

    private OpenBankingService BuildSubject() =>
        new(CurrentUserFor(Username), DbContextFactory, OpenBankingApi, NullLogger<OpenBankingService>.Instance,
            TimeProvider.System, _hybridCache, _openBankingSyncScheduler);
}
