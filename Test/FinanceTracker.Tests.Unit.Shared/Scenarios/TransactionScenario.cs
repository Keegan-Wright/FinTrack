using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class TransactionScenario : ScenarioBuilder
{
    public TransactionScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Imports a transaction onto the account.
    /// </summary>
    /// <param name="details">
    /// Generated when omitted, use <c>new TransactionDetailsFaker().Generate() with { ... }</c> to set specific values.
    /// </param>
    public Task<TestingScenario> WithTransaction(string transactionName, string accountName,
        TransactionDetails? details = null) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAccountAsync(context, accountName);
            BankTransaction transaction = BankTransaction.Import(account, details ?? new TransactionDetailsFaker().Generate());

            context.BankTransactions.Add(transaction);
            Scenario.TransactionMappings.Add(transactionName, transaction.Id);
        });

    /// <summary>
    /// Imports <paramref name="count"/> generated transactions onto the account.
    /// </summary>
    public Task<TestingScenario> WithTransactions(string accountName, int count) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAccountAsync(context, accountName);
            context.BankTransactions.AddRange(new TransactionDetailsFaker().Generate(count)
                .Select(x => BankTransaction.Import(account, x)));
        });

    public Task<TestingScenario> WithCustomClassification(string transactionName, string classificationName) =>
        PersistAsync(async context =>
        {
            Guid transactionId = Scenario.TransactionMappings[transactionName];
            Guid classificationId = Scenario.ClassificationMappings[classificationName];

            BankTransaction transaction = await context.BankTransactions
                .SingleAsync(x => x.Id == transactionId, Scenario.CancellationToken);
            CustomClassification classification = await context.CustomClassifications
                .SingleAsync(x => x.Id == classificationId, Scenario.CancellationToken);

            transaction.Classify(classification);
        });

    private Task<BankAccount> LoadAccountAsync(Infrastructure.FinanceTrackerContext context, string accountName)
    {
        Guid accountId = Scenario.AccountMappings[accountName];
        return context.BankAccounts
            .AsNoTracking()
            .SingleAsync(x => x.Id == accountId, Scenario.CancellationToken);
    }
}
