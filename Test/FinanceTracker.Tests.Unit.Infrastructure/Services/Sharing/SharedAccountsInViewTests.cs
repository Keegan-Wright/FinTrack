using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Infrastructure.Calendar;
using FinanceTracker.Infrastructure.Dashboard;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.NetWorth;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Reports;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// What a user sees of accounts shared with them in the money views, and that what isn't shared never shows. Sam
/// shares Joint with Alex and keeps Gift fund private, Jo isn't linked with either.
/// </summary>
public class SharedAccountsInViewTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";
    private const string Jo = "jo";

    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Spent = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly BalanceDay = new(2026, 3, 14);

    private ServiceProvider _services = null!;
    private Guid _linkId;

    [Before(Test)]
    public async Task SeedAsync()
    {
        List<(Guid UserId, NotificationKind Kind, string Title)> raised = [];
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();

        await Scenario.Do(s => s.UserScenario.WithUser("Alex", "Smith", "alex@example.test", Alex))
            .Do(s => s.UserScenario.WithUser("Sam", "Taylor", "sam@example.test", Sam))
            .Do(s => s.UserScenario.WithUser(Jo))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-current", "alex-bank", Named("Alex current")))
            .Do(s => s.TransactionScenario.WithTransaction("alex-coffee", "alex-current", Spending("Coffee", -3m)))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-private", "sam-bank", Named("Gift fund")))
            .Do(s => s.TransactionScenario.WithTransaction("sam-shop", "sam-joint", Spending("Groceries", -40m)))
            .Do(s => s.TransactionScenario.WithTransaction("sam-gift", "sam-private", Spending("Surprise", -100m, "Gifts")));

        await AddBalanceAsync("alex-current", 500m);
        await AddBalanceAsync("sam-joint", 1_000m);
        await AddBalanceAsync("sam-private", 5_000m);

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = ShareMode.ReadOnly }]
        }, CancellationToken);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    [Arguments(ShareMode.ReadOnly, false)]
    [Arguments(ShareMode.AllowChanges, true)]
    public async Task ChangeableIn_GivenTheWholeView_StillOnlyChangesAllowed(ShareMode mode, bool changeable)
    {
        // Arrange, the view of everything Alex sees, not the changeable one
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = mode }]
        }, CancellationToken);
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        AccountsInView all = await Scope(Alex).ResolveAllAsync(context, CancellationToken);

        // Act
        List<Guid> ids = await context.BankTransactions.ChangeableIn(all).Select(static x => x.Id)
            .ToListAsync(CancellationToken);

        // Assert, Alex's own always
        using (Assert.Multiple())
        {
            await Assert.That(ids).Contains(Scenario.TransactionMappings["alex-coffee"]);
            await Assert.That(ids.Contains(Scenario.TransactionMappings["sam-shop"])).IsEqualTo(changeable);
        }
    }

    [Test]
    public async Task Accounts_ShowTheSharedAccountAndWhoSharesIt_NeverThePrivateOne()
    {
        // Act
        List<AccountAndTransactions> accounts = await Accounts(Alex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(static x => (x.AccountName, x.SharedBy)))
                .IsEquivalentTo([("Alex current", (string?)null), ("Joint", "Sam Taylor")]);
            await Assert.That(accounts.Single(static x => x.AccountName == "Joint").Transactions!
                .Select(static x => x.Description)).IsEquivalentTo(["Groceries"]);
        }
    }

    [Test]
    public async Task Transactions_IncludeTheSharedAccounts_NeverThePrivateOnes()
    {
        // Act
        List<Transaction> transactions = await TransactionsSeenBy(Alex);

        // Assert
        await Assert.That(transactions.Select(static x => (x.Description, x.SharedBy)))
            .IsEquivalentTo([("Coffee", (string?)null), ("Groceries", "Sam Taylor")]);
    }

    [Test]
    public async Task TransactionFilters_OfferTheSharedAccount_NeverThePrivateOne()
    {
        // Act
        List<TransactionAccountFilter> accounts = await Transactions(Alex)
            .GetAccountsForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);
        List<TransactionCategoryFilter> categories = await Transactions(Alex)
            .GetCategoriesForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(static x => x.AccountName)).IsEquivalentTo(["Alex current", "Joint"]);
            await Assert.That(categories.Select(static x => x.TransactionCategory)).DoesNotContain("Gifts");
        }
    }

    [Test]
    public async Task JustMine_ShowsOnlyTheirOwn()
    {
        // Arrange
        await Sharing(Alex).ChangeViewAsync(new ChangeSharingView { IncludeShared = false }, CancellationToken);

        // Act
        List<AccountAndTransactions> accounts = await Accounts(Alex);
        List<Transaction> transactions = await TransactionsSeenBy(Alex);
        SharingView view = await Sharing(Alex).GetViewAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(static x => x.AccountName)).IsEquivalentTo(["Alex current"]);
            await Assert.That(transactions.Select(static x => x.Description)).IsEquivalentTo(["Coffee"]);
            await Assert.That(view.IncludesShared).IsFalse();
            await Assert.That(view.HasSharedAccounts).IsTrue();
        }
    }

    [Test]
    public async Task TheSharer_DoesntSeeTheOtherPersonsAccounts_TheyShareNothing()
    {
        // Act
        List<AccountAndTransactions> accounts = await Accounts(Sam);
        SharingView view = await Sharing(Sam).GetViewAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(static x => x.AccountName)).IsEquivalentTo(["Joint", "Gift fund"]);
            await Assert.That(view.HasSharedAccounts).IsFalse();
        }
    }

    [Test]
    public async Task SomeoneNotLinked_SeesNothingOfTheirs()
    {
        // Act
        List<AccountAndTransactions> accounts = await Accounts(Jo);
        List<Transaction> transactions = await TransactionsSeenBy(Jo);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts).IsEmpty();
            await Assert.That(transactions).IsEmpty();
        }
    }

    [Test]
    public async Task EndingTheLink_TakesTheSharedAccountAwayAtOnce()
    {
        // Arrange
        await Sharing(Alex).EndAsync(_linkId, CancellationToken);

        // Act
        List<AccountAndTransactions> accounts = await Accounts(Alex);
        List<Transaction> transactions = await TransactionsSeenBy(Alex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(static x => x.AccountName)).IsEquivalentTo(["Alex current"]);
            await Assert.That(transactions.Select(static x => x.Description)).IsEquivalentTo(["Coffee"]);
        }
    }

    [Test]
    public async Task Dashboard_CountsTheSharedAccountsSpending_NotThePrivateOnes()
    {
        // Act
        SpentInTimePeriod spent = await new DashboardService(CurrentUserFor(Alex), DbContextFactory,
                NullLogger<DashboardService>.Instance, new FixedTimeProvider(Now), Scope(Alex))
            .GetSpentInTimePeriod(Spent.AddDays(-1), Spent.AddDays(1), CancellationToken);

        // Assert
        await Assert.That(spent.TotalOut).IsEqualTo(-43m);
    }

    [Test]
    public async Task Reports_BreakDownTheSharedAccount_NeverThePrivateOne()
    {
        // Act
        List<SpentInAccountReport> report = await new ReportService(CurrentUserFor(Alex), DbContextFactory,
                NullLogger<ReportService>.Instance, new FixedTimeProvider(Now), Scope(Alex))
            .GetAccountBreakdownReportAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(report.Select(static x => x.AccountName)).IsEquivalentTo(["Alex current", "Joint"]);
    }

    [Test]
    public async Task Calendar_ShowsTheSharedAccountsTransactions_NeverThePrivateOnes()
    {
        // Arrange
        ICurrentUser alex = CurrentUserFor(Alex);
        FixedTimeProvider time = new(Now);
        CalendarService subject = new(alex, DbContextFactory, NullLogger<CalendarService>.Instance, time,
            new SavingsGoalsService(alex, DbContextFactory, NullLogger<SavingsGoalsService>.Instance, time),
            new DebtsService(alex, DbContextFactory, NullLogger<DebtsService>.Instance, time, Scope(Alex)), Scope(Alex));

        // Act
        List<CalendarItems> month = await subject.GetMonthItemsAsync(3, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(month.SelectMany(static x => x.Transactions).Select(static x => x.Description))
            .IsEquivalentTo(["Coffee", "Groceries"]);
    }

    [Test]
    public async Task NetWorth_AddsTheSharedAccountsBalance_NeverThePrivateOnes()
    {
        // Arrange
        NetWorthService subject = new(CurrentUserFor(Alex), DbContextFactory, NullLogger<NetWorthService>.Instance,
            new FixedTimeProvider(Now), Scope(Alex));

        // Act
        NetWorthHistory history = await subject.GetNetWorthAsync(null, CancellationToken);
        List<AccountBalanceHistory> balances = await subject.GetBalanceHistoriesAsync(null, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(history.Points.Single().NetWorth).IsEqualTo(1_500m);
            await Assert.That(history.Breakdown.Select(static x => (x.Name, x.Amount)))
                .IsEquivalentTo([("Joint", 1_000m), ("Alex current", 500m)]);
            await Assert.That(balances.Select(static x => x.AccountId))
                .IsEquivalentTo([Account("alex-current"), Account("sam-joint")]);
        }
    }

    [Test]
    public async Task ChangingASharedTransaction_IsRefused_ItStaysTheOwners()
    {
        // Act and assert, read only until changes are allowed
        await Assert.ThrowsAsync<InvalidOperationException>(() => Transactions(Alex).UnsplitAsync(
            Scenario.TransactionMappings["sam-shop"], CancellationToken));
    }

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static FinanceTracker.Domain.OpenBanking.Transactions.TransactionDetails Spending(string description,
        decimal amount, string category = "Shopping") =>
        new TransactionDetailsFaker().Generate() with
        {
            Description = description,
            Amount = amount,
            OccurredAt = Spent,
            Category = category
        };

    private async Task AddBalanceAsync(string accountName, decimal current)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid accountId = Account(accountName);
        BankAccount account = context.BankAccounts.Single(x => x.Id == accountId);
        BalanceSnapshot balance = new(account.UserId, accountId, BalanceDay,
            new AccountBalance(current, current, BalanceDay.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
            BalanceSnapshotSource.Synced, isStale: false);
        context.BalanceSnapshots.Add(balance);

        // Each owner's own daily total, as the snapshot job keeps it, from their own accounts only
        NetWorthSnapshot? total = context.NetWorthSnapshots.Local
            .SingleOrDefault(x => x.UserId == account.UserId && x.Date == BalanceDay);
        if (total is null && !context.NetWorthSnapshots.Any(x => x.UserId == account.UserId && x.Date == BalanceDay))
        {
            context.NetWorthSnapshots.Add(new NetWorthSnapshot(account.UserId, BalanceDay, [balance]));
        }

        await context.SaveChangesAsync(CancellationToken);
    }

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private Task<List<AccountAndTransactions>> Accounts(string username) =>
        new AccountService(CurrentUserFor(username), DbContextFactory, NullLogger<AccountService>.Instance,
                Scope(username))
            .GetAccountsAndMostRecentTransactionsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private TransactionsService Transactions(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance, TimeProvider.System,
            Scope(username));

    private Task<List<Transaction>> TransactionsSeenBy(string username) =>
        Transactions(username).GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
