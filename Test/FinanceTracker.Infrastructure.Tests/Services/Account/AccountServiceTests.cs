using FinanceTracker.Contracts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Infrastructure.Tests.Services.Account;

public class AccountServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_ReturnsSortedLatestTransactionsPerAccount()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"account-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        await SeedAccountAsync(factory, PrimaryUserId, "Main", _cancellationTokenSource.Token,
            Transaction("tx-1", "Older", -20, DateTime.UtcNow.AddDays(-2), category: "SHOPPING"),
            Transaction("tx-2", "Latest", -10, DateTime.UtcNow.AddDays(-1), category: "SHOPPING", isPending: true));

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
