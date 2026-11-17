using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.OpenBanking;

/// <summary>
/// The fake bank and the seeding and loading helpers shared by <see cref="OpenBankingServiceTests"/> and
/// <see cref="OpenBankingSyncServiceTests"/>.
/// </summary>
public abstract class OpenBankingTestBase : InfrastructureTestBase
{
    protected const string Username = "primary";
    protected const string OtherUsername = "secondary";
    protected const string Token = "active-token";

    protected IOpenBankingApiService OpenBankingApi { get; } = Substitute.For<IOpenBankingApiService>();

    [Before(Test)]
    public void SetupApi() => GivenProviderReturns();

    protected Task<TestingScenario> SeedAccountAsync(ExternalOpenBankingAccount externalAccount,
        TimeSpan authenticatedAgo = default) =>
        SeedConnectionAsync(authenticatedAgo: authenticatedAgo)
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, externalAccount.AccountId, "TRANSACTION", "Main", "GBP")));

    protected static TransactionDetails PendingDetails(string externalId) =>
        new(externalId, "Card purchase", TransactionType.Debit, "PURCHASE", -1m, "GBP", DateTime.UtcNow, true, [],
            TransactionMetadata.None, false);

    /// <param name="withCards">Whether the connection's consent covers cards.</param>
    /// <param name="authenticatedAgo">How long ago the user authenticated, just now unless given.</param>
    protected Task<TestingScenario> SeedConnectionAsync(bool withCards = false, TimeSpan authenticatedAgo = default) =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
            {
                List<string> scopes = [.. BankConnectionFaker.Scopes];
                if (withCards)
                {
                    scopes.Add("cards");
                }

                DateTime authenticatedAt = DateTime.UtcNow - authenticatedAgo;
                return new BankConnection(userId, "ob-bank", "Bank", "code", scopes, [1], authenticatedAt,
                    authenticatedAt.AddDays(90));
            }))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: Token));

    /// <summary>
    /// Sets the connection's cards and what the provider returns for each one, nothing when omitted.
    /// </summary>
    protected void GivenCardsReturn(ExternalOpenBankingCard[] cards,
        ExternalOpenBankingAccountBalance[]? balances = null,
        ExternalOpenBankingAccountTransaction[]? transactions = null)
    {
        OpenBankingApi.GetAllCardsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingListAllCardsResponse { Results = cards.ToAsyncEnumerable() });
        OpenBankingApi.GetCardBalanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
                new ExternalOpenBankingGetAccountBalanceResponse { Results = (balances ?? []).ToAsyncEnumerable() });
        OpenBankingApi.GetCardTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountTransactionsResponse { Results = (transactions ?? []).ToAsyncEnumerable() });
        OpenBankingApi.GetCardPendingTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountTransactionsResponse
                { Results = AsyncEnumerable.Empty<ExternalOpenBankingAccountTransaction>() });
    }

    /// <summary>
    /// A card transaction as TrueLayer is assumed to send it: a positive amount, its direction in the type.
    /// </summary>
    protected static ExternalOpenBankingAccountTransaction CardTransaction(string id, string type, decimal amount)
    {
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();
        transaction.TransactionId = id;
        transaction.TransactionType = type;
        transaction.Amount = amount;
        return transaction;
    }

    /// <summary>
    /// Sets what the provider returns for every account, nothing when omitted.
    /// </summary>
    protected void GivenProviderReturns(
        ExternalOpenBankingAccount[]? accounts = null,
        ExternalOpenBankingAccountBalance[]? balances = null,
        ExternalOpenBankingAccountStandingOrder[]? standingOrders = null,
        ExternalOpenBankingDirectDebit[]? directDebits = null,
        ExternalOpenBankingAccountTransaction[]? transactions = null,
        ExternalOpenBankingAccountTransaction[]? pendingTransactions = null)
    {
        OpenBankingApi.GetAllAccountsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingListAllAccountsResponse { Results = (accounts ?? []).ToAsyncEnumerable() });
        OpenBankingApi.GetAccountBalanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
                new ExternalOpenBankingGetAccountBalanceResponse { Results = (balances ?? []).ToAsyncEnumerable() });
        OpenBankingApi.GetAccountStandingOrdersAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
                new ExternalOpenBankingAccountStandingOrdersResponse
                    { Results = (standingOrders ?? []).ToAsyncEnumerable() });
        OpenBankingApi.GetAccountDirectDebitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
                new ExternalOpenBankingAccountDirectDebitsResponse
                    { Results = (directDebits ?? []).ToAsyncEnumerable() });
        OpenBankingApi.GetAccountTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountTransactionsResponse { Results = (transactions ?? []).ToAsyncEnumerable() });
        OpenBankingApi.GetAccountPendingTransactionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>()).Returns(_ =>
            new ExternalOpenBankingAccountTransactionsResponse
                { Results = (pendingTransactions ?? []).ToAsyncEnumerable() });
    }

    protected async Task<List<BankAccount>> LoadAccountsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankAccounts
            .Include(x => x.StandingOrders)
            .Include(x => x.DirectDebits)
            .ToListAsync(CancellationToken);
    }

    protected async Task<List<BankTransaction>> LoadTransactionsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.ToListAsync(CancellationToken);
    }

    protected async Task<List<SyncType>> LoadSyncLogTypesAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.SyncLog.Select(x => x.Type).ToListAsync(CancellationToken);
    }

    protected async Task<BankConnection> LoadConnectionAsync(string connectionName)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.ConnectionMappings[connectionName];
        return await context.BankConnections.Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == id, CancellationToken);
    }

    protected void GivenCodeExchangeFor(ExternalOpenBankingAccountConnection external)
    {
        OpenBankingApi.ExchangeCodeForAccessTokenAsync("new-code", CancellationToken).Returns(
            new ExternalOpenBankingAccessResponse { AccessToken = Token, RefreshToken = "refresh", ExpiresIn = 3600 });
        OpenBankingApi.GetProviderInformation(Token, CancellationToken).Returns(
            new ExternalOpenBankingAccountConnectionResponse
            {
                Results = new[] { external }.ToAsyncEnumerable()
            });
    }

    protected static Func<Guid, BankConnection> Named(string name) => userId =>
        new BankConnection(userId, $"ob-{Guid.NewGuid():N}", name, "code", ["accounts"], [1], DateTime.UtcNow,
            DateTime.UtcNow.AddDays(90));
}
