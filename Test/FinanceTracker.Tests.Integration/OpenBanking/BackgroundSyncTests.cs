using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillFrequency = FinanceTracker.Domain.Bills.BillFrequency;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Integration.OpenBanking;

/// <summary>
/// Runs the real background jobs through Quartz against <see cref="FakeTrueLayer"/>, started as the app starts them:
/// a scheduled job by an admin's run now, a user's sync by their sync now. The scheduled jobs visit every
/// connection in the database, so these tests don't run alongside each other, and the sync tests use the user's
/// sync but for <see cref="ScheduledSync_SyncsEveryUsersConnections"/>.
/// </summary>
[NotInParallel(nameof(BackgroundSyncTests))]
public class BackgroundSyncTests : IntegrationTestBase
{
    private const string SyncFunction = "SyncAllOpenBankingDetailsAsync";
    private const string UserSyncFunction = "SyncUserOpenBankingDetailsAsync";
    private const string MonitorFunction = "MonitorOpenBankingTokenExpirationAsync";
    private const string MoveGoalsOnFunction = "MoveEventSavingsGoalsOnAsync";

    [Test]
    public async Task Sync_ImportsAccountsAndTheirData()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));

        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        ExternalOpenBankingAccountBalance balance = new ExternalOpenBankingAccountBalanceFaker().Generate();
        List<ExternalOpenBankingAccountTransaction> transactions = new ExternalOpenBankingAccountTransactionFaker().Generate(3);
        ExternalOpenBankingAccountTransaction pending = new ExternalOpenBankingAccountTransactionFaker().Generate();
        ExternalOpenBankingAccountStandingOrder standingOrder = new ExternalOpenBankingAccountStandingOrderFaker().Generate();
        ExternalOpenBankingDirectDebit directDebit = new ExternalOpenBankingDirectDebitFaker().Generate();

        await App.TrueLayer.StubAccountAsync(accessToken, account, [balance], transactions, [pending],
            [standingOrder], [directDebit]);

        // Act
        await RunUserSyncAsync(user);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["bank"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        BankAccount synced = await context.BankAccounts
            .Include(x => x.StandingOrders)
            .Include(x => x.DirectDebits)
            .SingleAsync(x => x.ConnectionId == connectionId, CancellationToken);

        List<BankTransaction> syncedTransactions = await context.BankTransactions
            .Where(x => x.AccountId == synced.Id)
            .ToListAsync(CancellationToken);

        List<SyncType> syncLog = await context.SyncLog
            .Where(x => x.AccountId == synced.Id)
            .Select(x => x.Type)
            .ToListAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(synced.ExternalId).IsEqualTo(account.AccountId);
            await Assert.That(synced.DisplayName).IsEqualTo(account.DisplayName);
            await Assert.That(synced.Balance!.Current).IsEqualTo(balance.Current);
            await Assert.That(synced.Balance!.Available).IsEqualTo(balance.Available);
            await Assert.That(synced.StandingOrders).Count().IsEqualTo(1);
            await Assert.That(synced.DirectDebits).Count().IsEqualTo(1);
            await Assert.That(syncedTransactions.Select(x => x.ExternalId))
                .IsEquivalentTo(transactions.Append(pending).Select(x => x.TransactionId));
            await Assert.That(syncLog).IsEquivalentTo(
                [SyncType.Account, SyncType.Balance, SyncType.StandingOrders, SyncType.DirectDebits, SyncType.Transactions]);
        }
    }

    [Test]
    public async Task Sync_ASyncedStandingOrder_ShowsAsAnUpcomingPaymentOnTheDashboard()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));

        // Quarterly, so only the one payment falls in the dashboard's window
        DateTime nextPayment = DateTime.UtcNow.Date.AddDays(10).AddHours(12);
        ExternalOpenBankingAccountStandingOrder standingOrder = new ExternalOpenBankingAccountStandingOrderFaker().Generate();
        standingOrder.Frequency = "Quarterly";
        standingOrder.NextPaymentDate = nextPayment;

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate(),
            standingOrders: [standingOrder]);

        // Act
        await RunUserSyncAsync(user);
        List<UpcomingPayment> upcoming = await ApiFor(user).Get<IDashboardService>()
            .GetUpcomingPaymentsAsync(5, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        UpcomingPayment payment = upcoming.Single();
        using (Assert.Multiple())
        {
            await Assert.That(payment.PaymentName).IsEqualTo(standingOrder.Payee);
            await Assert.That(payment.PaymentType).IsEqualTo("Standing order");
            await Assert.That(payment.Amount).IsEqualTo(standingOrder.NextPaymentAmount);
            await Assert.That(payment.Due).IsEqualTo(DateOnly.FromDateTime(nextPayment));
        }
    }

    [Test]
    public async Task Sync_AStandingOrderAndItsPaymentTogether_ShowsThatPaymentAsPaid()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));

        DateTime lastPayment = DateTime.UtcNow.Date.AddDays(-2).AddHours(12);
        ExternalOpenBankingAccountStandingOrder standingOrder = WeeklyStandingOrder(lastPayment);
        ExternalOpenBankingAccountTransaction payment =
            Debit(standingOrder.Payee, -standingOrder.NextPaymentAmount, lastPayment);

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate(),
            transactions: [payment], standingOrders: [standingOrder]);

        // Act
        await RunUserSyncAsync(user);
        List<UpcomingPayment> upcoming = await ApiFor(user).Get<IDashboardService>()
            .GetUpcomingPaymentsAsync(5, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        UpcomingPayment paid = upcoming.Single(x => x.Due == DateOnly.FromDateTime(lastPayment));
        using (Assert.Multiple())
        {
            await Assert.That(paid.State).IsEqualTo(BillState.Paid);
            await Assert.That(paid.Amount).IsEqualTo(standingOrder.NextPaymentAmount);
        }
    }

    [Test]
    public async Task Sync_ARulePaysABill_TheMatcherLeavesThatPeriodToTheRule()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        DateTime due = DateTime.UtcNow.Date.AddDays(-2).AddHours(12);
        DomainBill? rent = null;

        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, account.AccountId, "TRANSACTION", "Main", "GBP")))
            .Do(s => s.BillScenario.WithBill("rent", user.Username, "main", (userId, accountId) =>
                rent = new DomainBill(userId, accountId, "Rent", "Landlord", DomainBillFrequency.Monthly, 650m,
                    DateOnly.FromDateTime(due), DomainBillSource.Manual)))
            .Do(s => s.RuleScenario.WithRule("rent", user.Username, userId =>
                new Rule(userId, "Rent share", 0, RuleMatchMode.All,
                    [new RuleCondition(RuleField.Description, RuleOperator.Contains, "RENT SHARE")],
                    [RuleAction.PayBill(rent!)])));

        // The matcher would pay the bill with the payment to the landlord, if it ran before the rule
        ExternalOpenBankingAccountTransaction rulesPayment = Debit("RENT SHARE", -650m, due);
        ExternalOpenBankingAccountTransaction landlordPayment = Debit("LANDLORD", -650m, due.AddHours(1));

        await App.TrueLayer.StubAccountAsync(accessToken, account, transactions: [rulesPayment, landlordPayment]);

        // Act
        await RunUserSyncAsync(user);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["bank"];
        BankTransaction paidByRule = await SyncedTransactionAsync(connectionId, rulesPayment.TransactionId);
        BankTransaction toLandlord = await SyncedTransactionAsync(connectionId, landlordPayment.TransactionId);

        using (Assert.Multiple())
        {
            await Assert.That(paidByRule.PaysBill(rent!.Id)).IsTrue();
            await Assert.That(paidByRule.WasDecidedBy(Scenario.RuleMappings["rent"])).IsTrue();
            await Assert.That(toLandlord.BillPayment).IsNull();
        }
    }

    [Test]
    public async Task Sync_ARulePaysOffADebt_ItOwesLessAndNetWorthCountsWhatsLeft()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        DateTime paidAt = DateTime.UtcNow.Date.AddDays(-2).AddHours(12);
        DomainDebt? loan = null;

        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken))
            .Do(s => s.DebtScenario.WithDebt("loan", user.Username, userId =>
                loan = new DomainDebt(userId, "Car loan", null, 1000m, DateOnly.FromDateTime(paidAt).AddDays(-10), 0m,
                    100m)))
            .Do(s => s.RuleScenario.WithRule("car finance", user.Username, userId =>
                new Rule(userId, "Car finance", 0, RuleMatchMode.All,
                    [new RuleCondition(RuleField.Description, RuleOperator.Contains, "CAR FINANCE")],
                    [RuleAction.PayDebt(loan!)])));

        ExternalOpenBankingAccountBalance balance = new ExternalOpenBankingAccountBalanceFaker().Generate();
        balance.Current = 500m;
        balance.Available = 500m;
        ExternalOpenBankingAccountTransaction payment = Debit("CAR FINANCE CO", -100m, paidAt);

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate(), [balance],
            transactions: [payment]);

        // Act
        await RunUserSyncAsync(user);
        Debt debt = (await ApiFor(user).Get<IDebtsService>().GetDebtsAsync(CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        Guid userId = Scenario.UserMappings[user.Username];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<NetWorthSnapshot> history = await context.NetWorthSnapshots.Where(x => x.UserId == userId)
            .OrderBy(static x => x.Date)
            .ToListAsync(CancellationToken);
        NetWorthSnapshot netWorth = history[^1];
        NetWorthSnapshot beforePaying = history.Single(x => x.Date == DateOnly.FromDateTime(paidAt).AddDays(-1));
        BankTransaction synced = await SyncedTransactionAsync(Scenario.ConnectionMappings["bank"],
            payment.TransactionId);

        using (Assert.Multiple())
        {
            await Assert.That(synced.PaysDebt(loan!.Id)).IsTrue();
            await Assert.That(debt.Owed).IsEqualTo(900m);
            await Assert.That(debt.Paid).IsEqualTo(100m);
            await Assert.That(netWorth.Assets).IsEqualTo(500m);
            await Assert.That(netWorth.Liabilities).IsEqualTo(900m);
            await Assert.That(netWorth.IsDerived).IsFalse();
            await Assert.That(beforePaying.Assets).IsEqualTo(600m);
            await Assert.That(beforePaying.Liabilities).IsEqualTo(1000m);
            await Assert.That(beforePaying.IsDerived).IsTrue();
        }
    }

    [Test]
    public async Task Sync_AfterTheUserSaysAPaymentPaysNoBill_LeavesItUnpaid()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));

        DateTime lastPayment = DateTime.UtcNow.Date.AddDays(-2).AddHours(12);
        ExternalOpenBankingAccountStandingOrder standingOrder = WeeklyStandingOrder(lastPayment);
        ExternalOpenBankingAccountTransaction payment =
            Debit(standingOrder.Payee, -standingOrder.NextPaymentAmount, lastPayment);

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate(),
            transactions: [payment], standingOrders: [standingOrder]);

        await RunUserSyncAsync(user);

        Guid connectionId = Scenario.ConnectionMappings["bank"];
        BankTransaction matched = await SyncedTransactionAsync(connectionId, payment.TransactionId);
        await ApiFor(user).Get<IBillsService>()
            .PayBillAsync(matched.Id, new PayBill { BillId = null }, CancellationToken);

        // Act
        await RunUserSyncAsync(user);

        // Assert
        BankTransaction resynced = await SyncedTransactionAsync(connectionId, payment.TransactionId);
        using (Assert.Multiple())
        {
            await Assert.That(matched.BillPayment!.Source).IsEqualTo(DecisionSource.Matcher);
            await Assert.That(resynced.BillPayment!.BillId).IsNull();
            await Assert.That(resynced.BillPayment!.Source).IsEqualTo(DecisionSource.User);
        }
    }

    [Test]
    public async Task Sync_WithAnExpiredToken_RefreshesItFirst()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string expiredToken = UniqueName("expired");
        string refreshToken = UniqueName("refresh");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", DateTime.UtcNow.AddHours(-2),
                expiresInSeconds: 3600, token: expiredToken, refreshToken: refreshToken));

        ExternalOpenBankingAccessResponse refreshed = new ExternalOpenBankingAccessResponseFaker().Generate();
        await App.TrueLayer.StubRefreshAsync(refreshToken, refreshed);

        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        await App.TrueLayer.StubAccountAsync(refreshed.AccessToken, account);

        // Act
        await RunUserSyncAsync(user);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["bank"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        BankConnection connection = await context.BankConnections
            .Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == connectionId, CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(App.TrueLayer.RefreshRequestsFor(refreshToken)).Count().IsEqualTo(1);
            await Assert.That(App.TrueLayer.RequestsWithToken(expiredToken)).IsEmpty();
            await Assert.That(App.TrueLayer.RequestsWithToken(refreshed.AccessToken)).IsNotEmpty();
            await Assert.That(connection.ActiveToken!.Token).IsEqualTo(refreshed.AccessToken);
            await Assert.That(connection.ActiveToken!.RefreshToken).IsEqualTo(refreshed.RefreshToken);
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == connectionId, CancellationToken)).IsTrue();
        }
    }

    /// <summary>
    /// The one test of the scheduled sync, which visits every connection in the database. The rest use the user's
    /// sync, the same job for only their connections, so they don't wait on what the other tests have connected.
    /// </summary>
    [Test]
    public async Task ScheduledSync_SyncsEveryUsersConnections()
    {
        // Arrange
        string username = UniqueName();
        string otherUsername = UniqueName();
        string accessToken = UniqueName("token");
        string otherAccessToken = UniqueName("token");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.UserScenario.WithUser(otherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("first", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("first", token: accessToken))
            .Do(s => s.BankConnectionScenario.WithConnection("second", otherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("second", token: otherAccessToken));

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate());
        await App.TrueLayer.StubAccountAsync(otherAccessToken, new ExternalOpenBankingAccountFaker().Generate());

        // Act
        await RunJobAsync(SyncFunction);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["first"];
        Guid otherConnectionId = Scenario.ConnectionMappings["second"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == connectionId, CancellationToken)).IsTrue();
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == otherConnectionId, CancellationToken)).IsTrue();
        }
    }

    [Test]
    public async Task UserSync_OnlySyncsTheRequestingUsersConnections()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string username = user.Username;
        string otherUsername = UniqueName();
        string accessToken = UniqueName("token");
        string otherAccessToken = UniqueName("token");
        await Scenario.Do(s => s.UserScenario.WithUser(otherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("mine", token: accessToken))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", otherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("theirs", token: otherAccessToken));

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate());
        await App.TrueLayer.StubAccountAsync(otherAccessToken, new ExternalOpenBankingAccountFaker().Generate());

        // Act
        await RunUserSyncAsync(user);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["mine"];
        Guid otherConnectionId = Scenario.ConnectionMappings["theirs"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == connectionId, CancellationToken)).IsTrue();
            await Assert.That(await context.BankAccounts.AnyAsync(x => x.ConnectionId == otherConnectionId, CancellationToken)).IsFalse();
            await Assert.That(App.TrueLayer.RequestsWithToken(otherAccessToken)).IsEmpty();
        }
    }

    [Test]
    public async Task ConsentMonitor_RevokesLapsedConsent()
    {
        // Arrange
        string username = UniqueName();
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", DateTime.UtcNow.AddDays(-1)));

        // Act
        await RunJobAsync(MonitorFunction);

        // Assert
        Guid connectionId = Scenario.ConnectionMappings["bank"];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        BankConnection connection = await context.BankConnections
            .Include(x => x.AccessTokens)
            .SingleAsync(x => x.Id == connectionId, CancellationToken);

        await Assert.That(connection.ActiveToken).IsNull();
    }

    [Test]
    public async Task EventGoalsJob_MovesAPassedYearlyEventsGoalOnToNextYear()
    {
        // Arrange
        string username = UniqueName();
        DateOnly lastTime = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        SavingsGoal? gifts = null;
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("gifts", username, userId =>
                gifts = new SavingsGoal(userId, "Gifts", 0m, 0m, 60m, lastTime.ToDateTime(TimeOnly.MinValue))))
            .Do(s => s.CalendarEventScenario.WithEvent("birthday", username, userId =>
            {
                CalendarEvent birthday = new(userId, "Birthday", lastTime.AddYears(-1), EventRecurrence.Yearly);
                birthday.SetSavingsTarget(100m);
                birthday.LinkSavingsGoal(gifts!);
                birthday.SetRollsGoalOn(true);
                return birthday;
            }));

        // Act
        await RunJobAsync(MoveGoalsOnFunction);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        SavingsGoal goal = await context.SavingsGoals.AsNoTracking()
            .SingleAsync(x => x.Id == Scenario.SavingsGoalMappings["gifts"], CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(goal.TargetDate).IsEqualTo(lastTime.AddYears(1).ToDateTime(TimeOnly.MinValue));
            await Assert.That(goal.Target).IsEqualTo(100m);
        }
    }

    /// <summary>
    /// A standing order last paid on <paramref name="lastPayment"/>. Weekly, so the dates don't depend on how long
    /// the month is.
    /// </summary>
    private static ExternalOpenBankingAccountStandingOrder WeeklyStandingOrder(DateTime lastPayment)
    {
        ExternalOpenBankingAccountStandingOrder standingOrder = new ExternalOpenBankingAccountStandingOrderFaker().Generate();
        standingOrder.Frequency = "Weekly";
        standingOrder.FirstPaymentDate = lastPayment.AddDays(-7);
        standingOrder.NextPaymentDate = lastPayment.AddDays(7);
        return standingOrder;
    }

    private static ExternalOpenBankingAccountTransaction Debit(string description, decimal amount, DateTime timestamp)
    {
        ExternalOpenBankingAccountTransaction transaction = new ExternalOpenBankingAccountTransactionFaker().Generate();
        transaction.Description = description;
        transaction.TransactionType = "DEBIT";
        transaction.Amount = amount;
        transaction.Timestamp = timestamp;
        return transaction;
    }

    /// <summary>
    /// The transaction the sync stored for <paramref name="externalId"/>, compared in memory as it's encrypted.
    /// </summary>
    private async Task<BankTransaction> SyncedTransactionAsync(Guid connectionId, string externalId)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        List<Guid> accountIds = await context.BankAccounts
            .Where(x => x.ConnectionId == connectionId)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        List<BankTransaction> transactions = await context.BankTransactions
            .Where(x => accountIds.Contains(x.AccountId))
            .ToListAsync(CancellationToken);

        return transactions.Single(x => x.ExternalId == externalId);
    }

    [Test]
    public async Task Sync_AfterTheirAlertsAreSet_TellsThemOfALowBalanceAndALargePayment()
    {
        // Arrange, £40 available and a £900 payment yesterday
        SignedInUser user = await RegisterUserAsync();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: accessToken));

        ExternalOpenBankingAccountBalance balance = new ExternalOpenBankingAccountBalanceFaker().Generate();
        balance.Current = 50m;
        balance.Available = 40m;
        ExternalOpenBankingAccountTransaction sofa = new ExternalOpenBankingAccountTransactionFaker().Generate();
        sofa.TransactionType = "DEBIT";
        sofa.TransactionCategory = "PURCHASE";
        sofa.Description = "SOFA SHOP";
        sofa.Amount = -900m;
        sofa.Timestamp = DateTime.UtcNow.AddDays(-1);
        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate(), [balance],
            [sofa]);

        // The account arrives with the first sync, then they say what to be told about
        await RunUserSyncAsync(user);
        INotificationsService notifications = ApiFor(user).Get<INotificationsService>();
        NotificationSettings settings = await notifications.GetSettingsAsync(CancellationToken);
        await notifications.ChangeAlertsAsync(new ChangeAlertSettings
        {
            BudgetWarningPercent = settings.BudgetWarningPercent,
            BillReminderDays = settings.BillReminderDays,
            LargeTransactionOver = 500m,
            LowBalance = [new LowBalanceThreshold { AccountId = settings.LowBalance.Single().AccountId, Below = 100m }]
        }, CancellationToken);

        // Act
        await RunUserSyncAsync(user);

        // Assert
        List<Notification> told = await notifications.GetNotificationsAsync(50, CancellationToken)
            .ToListAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(told.Select(static x => x.Kind))
                .IsEquivalentTo([NotificationKind.LowBalance, NotificationKind.LargeTransaction]);
            await Assert.That(told.Single(x => x.Kind == NotificationKind.LargeTransaction).Title)
                .EndsWith("paid to SOFA SHOP");
        }
    }

    /// <summary>
    /// Runs <paramref name="function"/> now as an admin would and waits for it to finish.
    /// </summary>
    private async Task RunJobAsync(string function)
    {
        IAutomationService service = ApiFor(await RegisterAdminAsync()).Get<IAutomationService>();
        HashSet<string> before = await RunIdsAsync(service, function);

        await service.RunNowAsync(function, CancellationToken);

        await WaitForRunAsync(service, function, before);
    }

    /// <summary>
    /// Syncs <paramref name="user"/>'s banks as their sync now does and waits for it to finish.
    /// </summary>
    private async Task RunUserSyncAsync(SignedInUser user)
    {
        IAutomationService admin = ApiFor(await RegisterAdminAsync()).Get<IAutomationService>();
        HashSet<string> before = await RunIdsAsync(admin, UserSyncFunction);

        await ApiFor(user).Get<IAutomationService>().RequestSyncAsync(CancellationToken);

        await WaitForRunAsync(admin, UserSyncFunction, before);
    }

    /// <summary>
    /// The runs of <paramref name="function"/> on the automation page now.
    /// </summary>
    private async Task<HashSet<string>> RunIdsAsync(IAutomationService service, string function)
    {
        AutomationRunPage page = await service.GetRunsAsync(function, page: 0, pageSize: 25, CancellationToken);
        return page.Runs.Select(static x => x.Id).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Waits for a manual run of <paramref name="function"/> that isn't one of <paramref name="before"/> to finish, as
    /// the automation page shows it.
    /// </summary>
    private async Task WaitForRunAsync(IAutomationService service, string function, HashSet<string> before)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);

        while (true)
        {
            AutomationRunPage page = await service.GetRunsAsync(function, page: 0, pageSize: 25, timeout.Token);
            AutomationRun? run = page.Runs.FirstOrDefault(x => x.IsManual && !before.Contains(x.Id));

            switch (run?.Status)
            {
                case AutomationRunStatus.Done:
                    return;
                case AutomationRunStatus.Failed or AutomationRunStatus.Cancelled or AutomationRunStatus.Skipped:
                    throw new InvalidOperationException($"{function} finished as {run.Status}: {run.Error}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token);
        }
    }
}
