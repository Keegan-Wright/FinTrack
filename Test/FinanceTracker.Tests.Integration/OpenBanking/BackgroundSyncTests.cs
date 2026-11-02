using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Debts;
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
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillFrequency = FinanceTracker.Domain.Bills.BillFrequency;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;

namespace FinanceTracker.Tests.Integration.OpenBanking;

/// <summary>
/// Runs the real background jobs through TickerQ against <see cref="FakeTrueLayer"/>. The scheduled jobs visit every
/// connection in the database, so these tests don't run alongside each other.
/// </summary>
[NotInParallel(nameof(BackgroundSyncTests))]
public class BackgroundSyncTests : IntegrationTestBase
{
    private const string SyncFunction = "SyncAllOpenBankingDetailsAsync";
    private const string UserSyncFunction = "SyncUserOpenBankingDetailsAsync";
    private const string MonitorFunction = "MonitorOpenBankingTokenExpirationAsync";
    private const string MoveGoalsOnFunction = "MoveEventSavingsGoalsOnAsync";
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(90);

    [Test]
    public async Task Sync_ImportsAccountsAndTheirData()
    {
        // Arrange
        string username = UniqueName();
        string accessToken = UniqueName("token");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
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
        await RunSyncAsync();

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
        await RunSyncAsync();
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
        await RunSyncAsync();
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
        await RunSyncAsync();

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
        await RunSyncAsync();
        Debt debt = (await ApiFor(user).Get<IDebtsService>().GetDebtsAsync(CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        Guid userId = Scenario.UserMappings[user.Username];
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NetWorthSnapshot netWorth = await context.NetWorthSnapshots.SingleAsync(x => x.UserId == userId,
            CancellationToken);
        BankTransaction synced = await SyncedTransactionAsync(Scenario.ConnectionMappings["bank"],
            payment.TransactionId);

        using (Assert.Multiple())
        {
            await Assert.That(synced.PaysDebt(loan!.Id)).IsTrue();
            await Assert.That(debt.Owed).IsEqualTo(900m);
            await Assert.That(debt.Paid).IsEqualTo(100m);
            await Assert.That(netWorth.Assets).IsEqualTo(500m);
            await Assert.That(netWorth.Liabilities).IsEqualTo(900m);
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

        await RunSyncAsync();

        Guid connectionId = Scenario.ConnectionMappings["bank"];
        BankTransaction matched = await SyncedTransactionAsync(connectionId, payment.TransactionId);
        await ApiFor(user).Get<IBillsService>()
            .PayBillAsync(matched.Id, new PayBill { BillId = null }, CancellationToken);

        // Act
        await RunSyncAsync();

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
        string username = UniqueName();
        string expiredToken = UniqueName("expired");
        string refreshToken = UniqueName("refresh");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", DateTime.UtcNow.AddHours(-2),
                expiresInSeconds: 3600, token: expiredToken, refreshToken: refreshToken));

        ExternalOpenBankingAccessResponse refreshed = new ExternalOpenBankingAccessResponseFaker().Generate();
        await App.TrueLayer.StubRefreshAsync(refreshToken, refreshed);

        ExternalOpenBankingAccount account = new ExternalOpenBankingAccountFaker().Generate();
        await App.TrueLayer.StubAccountAsync(refreshed.AccessToken, account);

        // Act
        await RunSyncAsync();

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

    [Test]
    public async Task UserSync_OnlySyncsTheRequestingUsersConnections()
    {
        // Arrange
        string username = UniqueName();
        string otherUsername = UniqueName();
        string accessToken = UniqueName("token");
        string otherAccessToken = UniqueName("token");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.UserScenario.WithUser(otherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("mine", token: accessToken))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", otherUsername))
            .Do(s => s.BankConnectionScenario.WithAccessToken("theirs", token: otherAccessToken));

        await App.TrueLayer.StubAccountAsync(accessToken, new ExternalOpenBankingAccountFaker().Generate());
        await App.TrueLayer.StubAccountAsync(otherAccessToken, new ExternalOpenBankingAccountFaker().Generate());

        // Matches the request the resync button queues, SyncUserOpenBankingDetailsRequest
        byte[] request = TickerHelper.CreateTickerRequest(new { UserId = Scenario.UserMappings[username] });

        // Act
        await RunJobAsync(UserSyncFunction, request);

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

    private Task RunSyncAsync() => RunJobAsync(SyncFunction);

    /// <summary>
    /// Queues <paramref name="function"/> the same way the app does and waits for TickerQ to finish it.
    /// </summary>
    private async Task RunJobAsync(string function, byte[]? request = null)
    {
        Guid tickerId = Guid.NewGuid();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            context.TimeTickerEntities.Add(new TimeTickerEntity
            {
                Id = tickerId,
                Function = function,
                Request = request,
                ExecutionTime = DateTime.UtcNow
            });
            await context.SaveChangesAsync(CancellationToken);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(JobTimeout);

        while (true)
        {
            await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(timeout.Token);
            TimeTickerEntity ticker = await context.TimeTickerEntities.AsNoTracking()
                .SingleAsync(x => x.Id == tickerId, timeout.Token);

            switch (ticker.Status)
            {
                case TickerStatus.Done or TickerStatus.DueDone:
                    return;
                case TickerStatus.Failed or TickerStatus.Cancelled or TickerStatus.Skipped:
                    throw new InvalidOperationException(
                        $"{function} finished as {ticker.Status}: {ticker.ExceptionMessage}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token);
        }
    }
}
