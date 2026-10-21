using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class BankAccountScenario : ScenarioBuilder
{
    public BankAccountScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds an account to the connection, owned by the connection's user.
    /// </summary>
    /// <param name="create">Builds the account from the user and connection ids, generated when omitted.</param>
    public Task<TestingScenario> WithAccount(string accountName, string connectionName,
        Func<Guid, Guid, BankAccount>? create = null) =>
        PersistAsync(async context =>
        {
            Guid connectionId = Scenario.ConnectionMappings[connectionName];
            Guid userId = await context.BankConnections
                .Where(x => x.Id == connectionId)
                .Select(x => x.UserId)
                .SingleAsync(Scenario.CancellationToken);

            BankAccount account = create?.Invoke(userId, connectionId)
                                  ?? new BankAccountFaker(userId, connectionId).Generate();

            context.BankAccounts.Add(account);
            Scenario.AccountMappings.Add(accountName, account.Id);
        });

    /// <param name="balance">Generated when omitted.</param>
    public Task<TestingScenario> WithBalance(string accountName, AccountBalance? balance = null) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAsync(context, accountName);
            balance ??= new AccountBalanceFaker().Generate();
            account.UpdateBalance(balance.Available, balance.Current, balance.AsOf);
        });

    /// <summary>
    /// Sets the account's standing orders, replacing any it already has.
    /// </summary>
    public Task<TestingScenario> WithStandingOrders(string accountName, params StandingOrderDetails[] standingOrders) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAsync(context, accountName);
            account.SyncStandingOrders(standingOrders);
        });

    /// <summary>
    /// Sets the account's direct debits, replacing any it already has.
    /// </summary>
    public Task<TestingScenario> WithDirectDebits(string accountName, params DirectDebitDetails[] directDebits) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAsync(context, accountName);
            account.SyncDirectDebits(directDebits);
        });

    public Task<TestingScenario> WithSyncLog(string accountName, SyncType type, DateTime occurredAt) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAsync(context, accountName);
            context.SyncLog.Add(account.RecordSync(type, occurredAt));
        });

    private Task<BankAccount> LoadAsync(FinanceTrackerContext context, string accountName)
    {
        Guid accountId = Scenario.AccountMappings[accountName];
        return context.BankAccounts
            .Include(x => x.StandingOrders)
            .Include(x => x.DirectDebits)
            .SingleAsync(x => x.Id == accountId, Scenario.CancellationToken);
    }
}
