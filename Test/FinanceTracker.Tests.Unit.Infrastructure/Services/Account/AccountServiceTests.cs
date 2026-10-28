using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Account;

public class AccountServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_ReturnsLatestTransactionsFirst()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("oldest", "main",
                new TransactionDetailsFaker().Generate() with { Description = "Oldest", OccurredAt = now.AddDays(-3) }))
            .Do(s => s.TransactionScenario.WithTransaction("latest", "main",
                new TransactionDetailsFaker().Generate() with { Description = "Latest", OccurredAt = now.AddDays(-1) }))
            .Do(s => s.TransactionScenario.WithTransaction("middle", "main",
                new TransactionDetailsFaker().Generate() with { Description = "Middle", OccurredAt = now.AddDays(-2) }));

        AccountService subject = BuildSubject(Username);

        // Act
        List<AccountAndTransactions> accounts = await subject
            .GetAccountsAndMostRecentTransactionsAsync(2, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(string.Join(",", accounts.Single().Transactions!.Select(x => x.Description)))
            .IsEqualTo("Latest,Middle");
    }

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_MapsAccountBalanceAndLogo()
    {
        // Arrange
        byte[] logo = [9, 8, 7];
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-bank", "Bank", "code", ["accounts"], logo, DateTime.UtcNow, DateTime.UtcNow.AddDays(90))))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-1", "TRANSACTION", "Main Account", "GBP")))
            .Do(s => s.BankAccountScenario.WithBalance("main", new AccountBalance(90m, 100m, DateTime.UtcNow)));

        AccountService subject = BuildSubject(Username);

        // Act
        AccountAndTransactions account = (await subject
            .GetAccountsAndMostRecentTransactionsAsync(5, CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(account.AccountName).IsEqualTo("Main Account");
            await Assert.That(account.AccountType).IsEqualTo("TRANSACTION");
            await Assert.That(account.AccountBalance).IsEqualTo(100m);
            await Assert.That(account.AvailableBalance).IsEqualTo(90m);
            await Assert.That(account.Logo).IsEquivalentTo(logo);
            await Assert.That(account.Transactions!.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_WhenAccountHasNoBalance_ReturnsZeroBalances()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

        AccountService subject = BuildSubject(Username);

        // Act
        AccountAndTransactions account = (await subject
            .GetAccountsAndMostRecentTransactionsAsync(5, CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(account.AccountBalance).IsEqualTo(0m);
            await Assert.That(account.AvailableBalance).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_MapsPendingStatus()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("pending", "main",
                new TransactionDetailsFaker().Generate() with { IsPending = true, Description = "Pending" }))
            .Do(s => s.TransactionScenario.WithTransaction("booked", "main",
                new TransactionDetailsFaker().Generate() with { IsPending = false, Description = "Booked" }));

        AccountService subject = BuildSubject(Username);

        // Act
        AccountAndTransactions account = (await subject
            .GetAccountsAndMostRecentTransactionsAsync(5, CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(account.Transactions!.Single(x => x.Description == "Pending").Status).IsEqualTo("Pending");
            await Assert.That(account.Transactions!.Single(x => x.Description == "Booked").Status).IsEqualTo("Complete");
        }
    }

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_GroupsTransactionsByAccount()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("current", "bank"))
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.TransactionScenario.WithTransactions("current", 3))
            .Do(s => s.TransactionScenario.WithTransactions("savings", 1));

        AccountService subject = BuildSubject(Username);

        // Act
        List<AccountAndTransactions> accounts = await subject
            .GetAccountsAndMostRecentTransactionsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(accounts.Select(x => x.Transactions!.Count)).IsEquivalentTo([3, 1]);
    }

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_OnlyReturnsCurrentUsersAccounts()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransactions("other", 2));

        AccountService subject = BuildSubject(Username);

        // Act
        List<AccountAndTransactions> accounts = await subject
            .GetAccountsAndMostRecentTransactionsAsync(5, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(accounts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAccountsAndMostRecentTransactionsAsync_WhenUserHasNoAccounts_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));

        AccountService subject = BuildSubject(Username);

        // Act
        List<AccountAndTransactions> accounts = await subject
            .GetAccountsAndMostRecentTransactionsAsync(1, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(accounts.Count).IsEqualTo(0);
    }

    private AccountService BuildSubject(string username) => new(CurrentUserFor(username), DbContextFactory, NullLogger<AccountService>.Instance);
}
