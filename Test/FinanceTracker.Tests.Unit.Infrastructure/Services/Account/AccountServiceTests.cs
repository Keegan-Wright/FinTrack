using System.Security.Claims;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Shared;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Org.BouncyCastle.Bcpg.Sig;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Account;

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


public class AccountServiceTestsV2 : InfrastructureTestBase
{
    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_ReturnsSortedLatestTransactionsPerAccount()
    {

        var usserId = Guid.NewGuid();
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, usserId.ToString())
        ], "test");

        var princ = new ClaimsPrincipal(identity);

        await Scenario.Do(_ => _.UserScenario.WithUser("Keegan", "Wright", "fsdfsfsfddf@sdfsdfsfdsdf.com", "kwright"));

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);


        BankConnection connection = new(usserId, $"provider-{Guid.NewGuid()}", "Test Bank", "access-code",
            ["accounts", "transactions"], [1, 2, 3]);
        BankAccount bankAccount = new(usserId, connection.Id, $"acc-{Guid.NewGuid()}", "CURRENT", "displayName", "GBP");

        context.BankConnections.Add(connection);
        context.BankAccounts.Add(bankAccount);
        context.BankTransactions.AddRange(BankTransaction.Import(bankAccount, new TransactionDetails(Guid.NewGuid().ToString(),"Testing", TransactionType.Credit, "Test", 323m, "GBP", DateTime.UtcNow, false, [])));

        await context.SaveChangesAsync(CancellationToken);


        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new AccountService(princ, DbContextFactory, openBanking, NullLogger<AccountService>.Instance);

        List<string> descriptions = [];
        await foreach (var account in subject.GetAccountsAndMostRecentTransactionsAsync(1, SyncTypes.All, CancellationToken))
        {
            descriptions.Add(account.Transactions!.Single().Description);
        }

        await Assert.That(descriptions).IsEquivalentTo(["Latest"]);
    }
}
