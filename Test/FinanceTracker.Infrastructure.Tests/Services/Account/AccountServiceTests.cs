using FinanceTracker.Contracts;
using FinanceTracker.Domain;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Infrastructure.Tests.Services.Account;

public class AccountServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_ReturnsSortedLatestTransactionsPerAccount()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"account-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser user = await context.Users.Include(x => x.Providers)
                .SingleAsync(x => x.Id == PrimaryUserId, _cancellationTokenSource.Token);

            var provider = new OpenBankingProvider
            {
                Name = "Test Bank",
                AccessCode = "access",
                OpenBankingProviderId = "provider-id",
                Logo = [1, 2, 3],
                Accounts = [],
                Scopes = [],
                Syncronisations = [],
                Transactions = []
            };

            var account = new OpenBankingAccount
            {
                OpenBankingAccountId = "acc-id",
                AccountType = "CURRENT",
                DisplayName = "Main",
                Currency = "GBP",
                ProviderId = provider.Id,
                Transactions = [],
                StandingOrders = [],
                DirectDebits = [],
                Syncronisations = []
            };

            account.AccountBalance = new OpenBankingAccountBalance
            {
                Currency = "GBP",
                Available = 120,
                Current = 100,
                AccountId = account.Id
            };

            account.Transactions!.Add(new OpenBankingTransaction
            {
                Description = "Older",
                TransactionType = "DEBIT",
                TransactionCategory = "SHOPPING",
                Amount = -20,
                Currency = "GBP",
                TransactionId = "tx-1",
                TransactionTime = DateTime.UtcNow.AddDays(-2),
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            account.Transactions.Add(new OpenBankingTransaction
            {
                Description = "Latest",
                TransactionType = "DEBIT",
                TransactionCategory = "SHOPPING",
                Amount = -10,
                Currency = "GBP",
                TransactionId = "tx-2",
                TransactionTime = DateTime.UtcNow.AddDays(-1),
                Pending = true,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            provider.Accounts!.Add(account);
            user.Providers!.Add(provider);
            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new AccountService(BuildUser(PrimaryUserId), factory, openBanking, NullLogger<AccountService>.Instance);

        List<string> descriptions = [];
        await foreach (var account in subject.GetAccountsAndMostRecentTransactionsAsync(1, SyncTypes.All, _cancellationTokenSource.Token))
        {
            descriptions.Add(account.Transactions!.Single().Description);
        }

        await Assert.That(descriptions).IsEquivalentTo(["Latest"]);
    }

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_WhenUserHasNoAccounts_ReturnsEmpty()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"account-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new AccountService(BuildUser(PrimaryUserId), factory, openBanking, NullLogger<AccountService>.Instance);

        var accounts = new List<string>();
        await foreach (var account in subject.GetAccountsAndMostRecentTransactionsAsync(1, SyncTypes.All,
                           _cancellationTokenSource.Token))
        {
            accounts.Add(account.AccountName);
        }

        await Assert.That(accounts.Count).IsEqualTo(0);
    }
}
