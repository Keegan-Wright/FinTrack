using FinanceTracker.Contracts;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.OpenBanking;

public class OpenBankingServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private const string Token = "active-token";

    private readonly IOpenBankingApiService _api = Substitute.For<IOpenBankingApiService>();

    [Before(Test)]
    public void SetupApi() => GivenProviderReturns();

    [Test]
    public async Task GetOpenBankingProvidersForClientAsync_ReturnsProvidersSortedByDisplayName()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        _api.GetAvailableProvidersAsync(CancellationToken).Returns(new[]
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
    public async Task GetOpenBankingProvidersForClientAsync_MapsProviderDetailsAndScopes()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        _api.GetAvailableProvidersAsync(CancellationToken).Returns(new[]
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
        _api.GetAvailableProvidersAsync(CancellationToken)
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
        _api.BuildAuthUrl(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<string>>()).Returns("https://example.test/auth");

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
            _api.Received(1).BuildAuthUrl(
                Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new[] { "provider-1" })),
                Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new[] { "accounts", "transactions" })));
        }
    }

    [Test]
    public async Task SyncConnectionAsync_WhenConnectionHasNoActiveToken_DoesNotCallProvider()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username));

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        await _api.DidNotReceiveWithAnyArgs().GetAllAccountsAsync(default!, default);
    }

    [Test]
    public async Task SyncConnectionAsync_WhenConnectionOwnedByAnotherUser_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("theirs"));

        OpenBankingService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.SyncConnectionAsync(Scenario.ConnectionMappings["theirs"], SyncTypes.All, CancellationToken));
    }

    [Test]
    public async Task SyncConnectionAsync_NewProviderAccounts_CreatesAccounts()
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccount external = new ExternalOpenBankingAccountFaker().Generate();
        GivenProviderReturns(accounts: [external]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        BankAccount account = (await LoadAccountsAsync()).Single();

        using (Assert.Multiple())
        {
            await Assert.That(account.UserId).IsEqualTo(Scenario.UserMappings[Username]);
            await Assert.That(account.ConnectionId).IsEqualTo(Scenario.ConnectionMappings["bank"]);
            await Assert.That(account.ExternalId).IsEqualTo(external.AccountId);
            await Assert.That(account.AccountType).IsEqualTo(external.AccountType);
            await Assert.That(account.DisplayName).IsEqualTo(external.DisplayName);
            await Assert.That(account.Currency).IsEqualTo(external.Currency);
            await _api.Received(1).GetAllAccountsAsync(Token, CancellationToken);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_KnownProviderAccount_UpdatesExistingAccount()
    {
        // Arrange
        await SeedConnectionAsync()
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-1", "TRANSACTION", "Old Name", "GBP")));

        GivenProviderReturns(accounts:
        [
            new ExternalOpenBankingAccount
            {
                AccountId = "acc-1", AccountType = "SAVINGS", DisplayName = "New Name", Currency = "EUR"
            }
        ]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        BankAccount account = (await LoadAccountsAsync()).Single();

        using (Assert.Multiple())
        {
            await Assert.That(account.Id).IsEqualTo(Scenario.AccountMappings["main"]);
            await Assert.That(account.AccountType).IsEqualTo("SAVINGS");
            await Assert.That(account.DisplayName).IsEqualTo("New Name");
            await Assert.That(account.Currency).IsEqualTo("EUR");
        }
    }

    [Test]
    public async Task SyncConnectionAsync_AppliesBalanceStandingOrdersAndDirectDebits()
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccountBalance balance = new ExternalOpenBankingAccountBalanceFaker().Generate();
        ExternalOpenBankingAccountStandingOrder standingOrder = new ExternalOpenBankingAccountStandingOrderFaker().Generate();
        ExternalOpenBankingDirectDebit directDebit = new ExternalOpenBankingDirectDebitFaker().Generate();

        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()], balances: [balance],
            standingOrders: [standingOrder], directDebits: [directDebit]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        BankAccount account = (await LoadAccountsAsync()).Single();
        StandingOrder storedStandingOrder = account.StandingOrders.Single();
        DirectDebit storedDirectDebit = account.DirectDebits.Single();

        using (Assert.Multiple())
        {
            await Assert.That(account.Balance!.Available).IsEqualTo(balance.Available);
            await Assert.That(account.Balance.Current).IsEqualTo(balance.Current);
            await Assert.That(account.Balance.AsOf).IsEqualTo(balance.UpdateTimestamp);
            await Assert.That(storedStandingOrder.Reference).IsEqualTo(standingOrder.Reference);
            await Assert.That(storedStandingOrder.Payee).IsEqualTo(standingOrder.Payee);
            await Assert.That(storedStandingOrder.NextPaymentAmount).IsEqualTo(standingOrder.NextPaymentAmount);
            await Assert.That(storedStandingOrder.NextPaymentDate).IsEqualTo(standingOrder.NextPaymentDate);
            await Assert.That(storedDirectDebit.ExternalId).IsEqualTo(directDebit.DirectDebitId);
            await Assert.That(storedDirectDebit.Name).IsEqualTo(directDebit.Name);
            await Assert.That(storedDirectDebit.PreviousPaymentAmount).IsEqualTo(directDebit.PreviousPaymentAmount);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_UnspecifiedProviderTimestamps_AreStoredAsUtc()
    {
        // Arrange
        await SeedConnectionAsync();
        DateTime unspecified = new(2026, 2, 1, 8, 0, 0, DateTimeKind.Unspecified);

        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()],
            balances: [new ExternalOpenBankingAccountBalance { Available = 1, Current = 1, UpdateTimestamp = unspecified }]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        AccountBalance balance = (await LoadAccountsAsync()).Single().Balance!;

        using (Assert.Multiple())
        {
            await Assert.That(balance.AsOf.Kind).IsEqualTo(DateTimeKind.Utc);
            await Assert.That(balance.AsOf.Ticks).IsEqualTo(unspecified.Ticks);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_ImportsBookedAndPendingTransactions()
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccountTransaction booked = new ExternalOpenBankingAccountTransactionFaker().Generate();
        ExternalOpenBankingAccountTransaction pending = new ExternalOpenBankingAccountTransactionFaker().Generate();

        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()], transactions: [booked],
            pendingTransactions: [pending]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        List<BankTransaction> transactions = await LoadTransactionsAsync();
        BankTransaction storedBooked = transactions.Single(x => x.ExternalId == booked.TransactionId);
        BankTransaction storedPending = transactions.Single(x => x.ExternalId == pending.TransactionId);

        using (Assert.Multiple())
        {
            await Assert.That(transactions.Count).IsEqualTo(2);
            await Assert.That(storedBooked.IsPending).IsFalse();
            await Assert.That(storedBooked.Description).IsEqualTo(booked.Description);
            await Assert.That(storedBooked.Amount).IsEqualTo(booked.Amount);
            await Assert.That(storedBooked.Category).IsEqualTo(booked.TransactionCategory);
            await Assert.That(storedBooked.OccurredAt).IsEqualTo(booked.Timestamp);
            await Assert.That(storedBooked.ProviderClassifications)
                .IsEquivalentTo(await booked.TransactionClassification!.ToListAsync(CancellationToken));
            await Assert.That(storedPending.IsPending).IsTrue();
        }
    }

    [Test]
    public async Task SyncConnectionAsync_TransactionReportedAsPendingAndBooked_IsStoredAsBooked()
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();

        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()], transactions: [transaction],
            pendingTransactions: [transaction]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        BankTransaction stored = (await LoadTransactionsAsync()).Single();
        await Assert.That(stored.IsPending).IsFalse();
    }

    [Test]
    public async Task SyncConnectionAsync_PreviouslyPendingTransaction_IsUpdatedNotDuplicated()
    {
        // Arrange
        ExternalOpenBankingAccount externalAccount = new ExternalOpenBankingAccountFaker().Generate();
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();

        await SeedConnectionAsync()
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, externalAccount.AccountId, "TRANSACTION", "Main", "GBP")))
            .Do(s => s.TransactionScenario.WithTransaction("pending", "main", new TransactionDetails(
                transaction.TransactionId, "Pending description", TransactionType.Debit, "PURCHASE", -1m, "GBP",
                DateTime.UtcNow, true, [])));

        GivenProviderReturns(accounts: [externalAccount], transactions: [transaction]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        BankTransaction stored = (await LoadTransactionsAsync()).Single();

        using (Assert.Multiple())
        {
            await Assert.That(stored.Id).IsEqualTo(Scenario.TransactionMappings["pending"]);
            await Assert.That(stored.IsPending).IsFalse();
            await Assert.That(stored.Description).IsEqualTo(transaction.Description);
            await Assert.That(stored.Amount).IsEqualTo(transaction.Amount);
        }
    }

    [Test]
    [Arguments("DEBIT", 10, TransactionType.Debit)]
    [Arguments("credit", -10, TransactionType.Credit)]
    [Arguments("", -10, TransactionType.Debit)]
    [Arguments("UNKNOWN", 10, TransactionType.Credit)]
    public async Task SyncConnectionAsync_TransactionType_ParsedOrInferredFromAmount(string providerType, decimal amount,
        TransactionType expected)
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();
        transaction.TransactionType = providerType;
        transaction.Amount = amount;

        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()], transactions: [transaction]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        await Assert.That((await LoadTransactionsAsync()).Single().Type).IsEqualTo(expected);
    }

    [Test]
    public async Task SyncConnectionAsync_RecordsSyncLogForEachKindOfDataSynced()
    {
        // Arrange
        await SeedConnectionAsync();
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        await Assert.That(await LoadSyncLogTypesAsync()).IsEquivalentTo([
            SyncType.Account, SyncType.Balance, SyncType.StandingOrders, SyncType.DirectDebits,
            SyncType.Transactions
        ]);
    }

    [Test]
    public async Task SyncConnectionAsync_OnlyBalanceRequested_FetchesAndLogsBalanceOnly()
    {
        // Arrange
        await SeedConnectionAsync();
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.Balance, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await _api.ReceivedWithAnyArgs(1).GetAccountBalanceAsync(default!, default!, default);
            await _api.DidNotReceiveWithAnyArgs().GetAccountStandingOrdersAsync(default!, default!, default);
            await _api.DidNotReceiveWithAnyArgs().GetAccountDirectDebitsAsync(default!, default!, default);
            await _api.DidNotReceiveWithAnyArgs().GetAccountTransactionsAsync(default!, default!, default, default);
            await _api.DidNotReceiveWithAnyArgs().GetAccountPendingTransactionsAsync(default!, default!, default, default);
            await Assert.That(await LoadSyncLogTypesAsync()).IsEquivalentTo([SyncType.Account, SyncType.Balance]);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_TransactionsRequested_AlsoFetchesPendingTransactions()
    {
        // Arrange
        await SeedConnectionAsync();
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.Transactions, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await _api.ReceivedWithAnyArgs(1).GetAccountTransactionsAsync(default!, default!, default, default);
            await _api.ReceivedWithAnyArgs(1).GetAccountPendingTransactionsAsync(default!, default!, default, default);
            await Assert.That(await LoadSyncLogTypesAsync()).IsEquivalentTo([
                SyncType.Account, SyncType.Transactions
            ]);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_WhenOneProviderCallFails_AppliesTheRest()
    {
        // Arrange
        await SeedConnectionAsync();
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()],
            standingOrders: [new ExternalOpenBankingAccountStandingOrderFaker().Generate()]);
        _api.GetAccountBalanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("provider down"));

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        BankAccount account = (await LoadAccountsAsync()).Single();

        using (Assert.Multiple())
        {
            await Assert.That(account.Balance).IsNull();
            await Assert.That(account.StandingOrders.Count).IsEqualTo(1);
            await Assert.That(await LoadSyncLogTypesAsync()).DoesNotContain(SyncType.Balance);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_WithPreviousTransactionSync_RequestsFromSevenDaysEarlier()
    {
        // Arrange
        ExternalOpenBankingAccount externalAccount = new ExternalOpenBankingAccountFaker().Generate();
        DateTime lastSync = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        await SeedConnectionAsync()
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, externalAccount.AccountId, "TRANSACTION", "Main", "GBP")))
            .Do(s => s.BankAccountScenario.WithSyncLog("main", SyncType.Transactions, lastSync.AddDays(-30)))
            .Do(s => s.BankAccountScenario.WithSyncLog("main", SyncType.Transactions, lastSync))
            .Do(s => s.BankAccountScenario.WithSyncLog("main", SyncType.Balance, lastSync.AddDays(5)));

        GivenProviderReturns(accounts: [externalAccount]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        DateTime? expectedFrom = lastSync.AddDays(-7);

        using (Assert.Multiple())
        {
            await _api.Received(1).GetAccountTransactionsAsync(externalAccount.AccountId, Token,
                Arg.Is<DateTime?>(x => x == expectedFrom), CancellationToken);
            await _api.Received(1).GetAccountPendingTransactionsAsync(externalAccount.AccountId, Token,
                Arg.Is<DateTime?>(x => x == expectedFrom), CancellationToken);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_WithoutPreviousTransactionSync_RequestsFullHistory()
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccount externalAccount = new ExternalOpenBankingAccountFaker().Generate();
        GivenProviderReturns(accounts: [externalAccount]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        await _api.Received(1).GetAccountTransactionsAsync(externalAccount.AccountId, Token, null, CancellationToken);
    }

    [Test]
    public async Task SyncConnectionAsync_ExpiredToken_RefreshesPersistsAndUsesNewToken()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", DateTime.UtcNow.AddHours(-2), 3600, Token,
                "old-refresh"));

        _api.GetAccessTokenByRefreshTokenAsync("old-refresh", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = "new-token", RefreshToken = "new-refresh", ExpiresIn = 3600 });

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        BankConnection connection = await LoadConnectionAsync("bank");

        using (Assert.Multiple())
        {
            await _api.Received(1).GetAllAccountsAsync("new-token", CancellationToken);
            await Assert.That(connection.AccessTokens.Count).IsEqualTo(2);
            await Assert.That(connection.ActiveToken!.Token).IsEqualTo("new-token");
            await Assert.That(connection.ActiveToken.RefreshToken).IsEqualTo("new-refresh");
            await Assert.That(connection.AccessTokens.Single(x => x.Token == Token).IsRevoked).IsTrue();
        }
    }

    [Test]
    public async Task SyncConnectionAsync_ValidToken_DoesNotRefresh()
    {
        // Arrange
        await SeedConnectionAsync();
        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        await _api.DidNotReceiveWithAnyArgs().GetAccessTokenByRefreshTokenAsync(default!, default);
    }

    [Test]
    public async Task PerformSyncAsync_SyncsEachOfTheCurrentUsersConnections()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("first", token: "first-token"))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("second", token: "second-token"))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("theirs", token: "their-token"));

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.PerformSyncAsync(SyncTypes.All, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await _api.Received(1).GetAllAccountsAsync("first-token", CancellationToken);
            await _api.Received(1).GetAllAccountsAsync("second-token", CancellationToken);
            await _api.DidNotReceive().GetAllAccountsAsync("their-token", Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ExistingConnection_ReauthenticatesIssuesTokenAndSyncs()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow)))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: "old-token"));

        _api.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new[] { new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate() }.ToAsyncEnumerable()
        });

        OpenBankingService subject = BuildSubject();

        // Act
        bool result = await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

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
            await _api.Received(1).GetAllAccountsAsync(Token, CancellationToken);
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_NewProvider_CreatesConnectionWithLogoAndTokenThenSyncs()
    {
        // Arrange
        byte[] logo = [137, 80, 78, 71];
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        ExternalOpenBankingAccountConnection external = new ExternalOpenBankingAccountConnectionFaker("ob-new").Generate();

        _api.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new[] { external }.ToAsyncEnumerable()
        });
        _api.GetProviderLogoAsync(external.Provider!.LogoUri, CancellationToken).Returns(logo);
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()]);

        OpenBankingService subject = BuildSubject();
        DateTime before = DateTime.UtcNow;

        // Act
        bool result = await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection connection = await context.BankConnections.Include(x => x.AccessTokens)
            .SingleAsync(CancellationToken);
        BankAccount account = await context.BankAccounts.SingleAsync(CancellationToken);

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
            await Assert.That(account.ConnectionId).IsEqualTo(connection.Id);
            await _api.Received(1).GetAllAccountsAsync(Token, CancellationToken);
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_SeveralProvidersOnOneConsent_CreatesAndSyncsEach()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));

        _api.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new ExternalOpenBankingAccountConnectionFaker().Generate(2).ToAsyncEnumerable()
        });
        _api.GetProviderLogoAsync(Arg.Any<string>(), CancellationToken).Returns([1]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await context.BankConnections.CountAsync(CancellationToken)).IsEqualTo(2);
            await _api.Received(2).GetAllAccountsAsync(Token, CancellationToken);
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ExistingConnection_DoesNotDownloadLogo()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow)));

        _api.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new[] { new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate() }.ToAsyncEnumerable()
        });

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await _api.DidNotReceiveWithAnyArgs().GetProviderLogoAsync(default!, default);
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_LogoDownloadFails_ThrowsAndSavesNothing()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));

        _api.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new[] { new ExternalOpenBankingAccountConnectionFaker().Generate() }.ToAsyncEnumerable()
        });
        _api.GetProviderLogoAsync(Arg.Any<string>(), CancellationToken).ThrowsAsync(new HttpRequestException("404"));

        OpenBankingService subject = BuildSubject();

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(() =>

            subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That(await context.BankConnections.CountAsync(CancellationToken)).IsEqualTo(0);
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ExistingConnection_PersistsNewScopes()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-existing", "Bank", "old-code", ["accounts"], [1], DateTime.UtcNow)));

        _api.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new[] { new ExternalOpenBankingAccountConnectionFaker("ob-existing").Generate() }.ToAsyncEnumerable()
        });

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        BankConnection connection = await LoadConnectionAsync("bank");
        await Assert.That(connection.Scopes).IsEquivalentTo(Shared.Fakers.BankConnectionFaker.Scopes);
    }

    [Test]
    public async Task SyncConnectionAsync_ExistingTransaction_PersistsUpdatedProviderClassifications()
    {
        // Arrange
        ExternalOpenBankingAccount externalAccount = new ExternalOpenBankingAccountFaker().Generate();
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();
        transaction.TransactionClassification = new[] { "New" }.ToAsyncEnumerable();

        await SeedConnectionAsync()
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, externalAccount.AccountId, "TRANSACTION", "Main", "GBP")))
            .Do(s => s.TransactionScenario.WithTransaction("existing", "main", new TransactionDetails(
                transaction.TransactionId, "Description", TransactionType.Debit, "PURCHASE", -1m, "GBP",
                DateTime.UtcNow, false, ["Old"])));

        GivenProviderReturns(accounts: [externalAccount], transactions: [transaction]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        await Assert.That((await LoadTransactionsAsync()).Single().ProviderClassifications).IsEquivalentTo(["New"]);
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ProviderReturnsNoConnections_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        _api.ExchangeCodeForAccessTokenAsync("code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse());

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
        _api.ExchangeCodeForAccessTokenAsync("code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new[] { new ExternalOpenBankingAccountConnection { Provider = null } }.ToAsyncEnumerable()
        });

        OpenBankingService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "code" }, CancellationToken));
    }

    [Test]
    [Arguments("Balance", "account balance", SyncType.Balance)]
    [Arguments("StandingOrders", "syncing standing orders", SyncType.StandingOrders)]
    [Arguments("DirectDebits", "syncing direct debits", SyncType.DirectDebits)]
    [Arguments("Transactions", "syncing transactions", SyncType.Transactions)]
    [Arguments("PendingTransactions", "syncing pending transactions", null)]
    public async Task SyncConnectionAsync_ProviderCallFails_LogsErrorAndSkipsOnlyThatData(string failingCall,
        string expectedMessage, SyncType? skippedLog)
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccountTransaction booked = new ExternalOpenBankingAccountTransactionFaker().Generate();
        ExternalOpenBankingAccountTransaction pending = new ExternalOpenBankingAccountTransactionFaker().Generate();
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()],
            transactions: [booked], pendingTransactions: [pending]);

        HttpRequestException failure = new("provider down");
        switch (failingCall)
        {
            case "Balance":
                _api.GetAccountBalanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .ThrowsAsync(failure);
                break;
            case "StandingOrders":
                _api.GetAccountStandingOrdersAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .ThrowsAsync(failure);
                break;
            case "DirectDebits":
                _api.GetAccountDirectDebitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .ThrowsAsync(failure);
                break;
            case "Transactions":
                _api.GetAccountTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
                    Arg.Any<CancellationToken>()).ThrowsAsync(failure);
                break;
            case "PendingTransactions":
                _api.GetAccountPendingTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
                    Arg.Any<CancellationToken>()).ThrowsAsync(failure);
                break;
        }

        CapturingLogger<OpenBankingService> logger = new();
        OpenBankingService subject = new(CurrentUserFor(Username), DbContextFactory, _api, logger, TimeProvider.System);

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        (LogLevel Level, string Message, Exception? Exception) error = logger.Entries.Single(x => x.Level == Microsoft.Extensions.Logging.LogLevel.Error);
        List<SyncType> logged = await LoadSyncLogTypesAsync();
        List<string> imported = (await LoadTransactionsAsync()).Select(x => x.ExternalId).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(error.Message).Contains(expectedMessage);
            await Assert.That(error.Exception).IsSameReferenceAs(failure);
            await Assert.That(logged).Contains(SyncType.Account);
            if (skippedLog is not null)
            {
                await Assert.That(logged).DoesNotContain(skippedLog.Value);
            }

            // Whichever transaction list still arrived is imported
            await Assert.That(imported.Contains(booked.TransactionId)).IsEqualTo(failingCall != "Transactions");
            await Assert.That(imported.Contains(pending.TransactionId)).IsEqualTo(failingCall != "PendingTransactions");
        }
    }

    [Test]
    public async Task SyncConnectionAsync_ProviderReturnsNoAccountList_SyncsNothing()
    {
        // Arrange
        await SeedConnectionAsync();
        _api.GetAllAccountsAsync(Token, CancellationToken).Returns(new ExternalOpenBankingListAllAccountsResponse());

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await LoadAccountsAsync()).Count).IsEqualTo(0);
            await Assert.That((await LoadSyncLogTypesAsync()).Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_ProviderReturnsNoResults_TreatedAsEmptyAndLogged()
    {
        // Arrange
        await SeedConnectionAsync();
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()]);
        _api.GetAccountBalanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalOpenBankingGetAccountBalanceResponse());

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await LoadAccountsAsync()).Single().Balance).IsNull();
            await Assert.That(await LoadSyncLogTypesAsync()).Contains(SyncType.Balance);
        }
    }

    [Test]
    public async Task SyncConnectionAsync_TransactionWithoutClassifications_ImportsWithNone()
    {
        // Arrange
        await SeedConnectionAsync();
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();
        transaction.TransactionClassification = null;
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()], transactions: [transaction]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        await Assert.That((await LoadTransactionsAsync()).Single().ProviderClassifications.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SyncConnectionAsync_LocalProviderTimestamps_AreConvertedToUtc()
    {
        // Arrange
        await SeedConnectionAsync();
        DateTime local = new(2026, 2, 1, 8, 0, 0, DateTimeKind.Local);
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();
        transaction.Timestamp = local;
        GivenProviderReturns(accounts: [new ExternalOpenBankingAccountFaker().Generate()], transactions: [transaction]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.SyncConnectionAsync(Scenario.ConnectionMappings["bank"], SyncTypes.All, CancellationToken);

        // Assert
        DateTime stored = (await LoadTransactionsAsync()).Single().OccurredAt;

        using (Assert.Multiple())
        {
            await Assert.That(stored.Kind).IsEqualTo(DateTimeKind.Utc);
            await Assert.That(stored).IsEqualTo(local.ToUniversalTime());
        }
    }

    [Test]
    public async Task AddVendorViaAccessCodeAsync_ProviderReportsNoScopes_CreatesConnectionWithoutScopes()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        ExternalOpenBankingAccountConnection external = new ExternalOpenBankingAccountConnectionFaker().Generate();
        external.Scopes = null;

        _api.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        _api.GetProviderInformation(Token, CancellationToken).Returns(new ExternalOpenBankingAccountConnectionResponse
        {
            Results = new[] { external }.ToAsyncEnumerable()
        });
        _api.GetProviderLogoAsync(Arg.Any<string>(), CancellationToken).Returns([1]);

        OpenBankingService subject = BuildSubject();

        // Act
        await subject.AddVendorViaAccessCodeAsync(new AddVendor { AccessCode = "new-code" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That((await context.BankConnections.SingleAsync(CancellationToken)).Scopes.Count).IsEqualTo(0);
    }

    private Task<TestingScenario> SeedConnectionAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: Token));

    /// <summary>
    /// Sets what the provider returns for every account, nothing when omitted.
    /// </summary>
    private void GivenProviderReturns(
        ExternalOpenBankingAccount[]? accounts = null,
        ExternalOpenBankingAccountBalance[]? balances = null,
        ExternalOpenBankingAccountStandingOrder[]? standingOrders = null,
        ExternalOpenBankingDirectDebit[]? directDebits = null,
        ExternalOpenBankingAccountTransaction[]? transactions = null,
        ExternalOpenBankingAccountTransaction[]? pendingTransactions = null)
    {
        _api.GetAllAccountsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingListAllAccountsResponse { Results = (accounts ?? []).ToAsyncEnumerable() });
        _api.GetAccountBalanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingGetAccountBalanceResponse { Results = (balances ?? []).ToAsyncEnumerable() });
        _api.GetAccountStandingOrdersAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountStandingOrdersResponse { Results = (standingOrders ?? []).ToAsyncEnumerable() });
        _api.GetAccountDirectDebitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountDirectDebitsResponse { Results = (directDebits ?? []).ToAsyncEnumerable() });
        _api.GetAccountTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountTransactionsResponse { Results = (transactions ?? []).ToAsyncEnumerable() });
        _api.GetAccountPendingTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountTransactionsResponse { Results = (pendingTransactions ?? []).ToAsyncEnumerable() });
    }

    private async Task<List<BankAccount>> LoadAccountsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankAccounts
            .Include(x => x.StandingOrders)
            .Include(x => x.DirectDebits)
            .ToListAsync(CancellationToken);
    }

    private async Task<List<BankTransaction>> LoadTransactionsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.ToListAsync(CancellationToken);
    }

    private async Task<List<SyncType>> LoadSyncLogTypesAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.SyncLog.Select(x => x.Type).ToListAsync(CancellationToken);
    }

    private async Task<BankConnection> LoadConnectionAsync(string connectionName)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.ConnectionMappings[connectionName];
        return await context.BankConnections.Include(x => x.AccessTokens).SingleAsync(x => x.Id == id, CancellationToken);
    }

    private OpenBankingService BuildSubject() =>
        new(CurrentUserFor(Username), DbContextFactory, _api, NullLogger<OpenBankingService>.Instance,
            TimeProvider.System);
}
