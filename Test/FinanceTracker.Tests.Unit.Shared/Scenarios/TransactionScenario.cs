using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
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
        WithTransactions(accountName, new TransactionDetailsFaker().Generate(count));

    /// <summary>
    /// Imports each of <paramref name="details"/> into the account, unnamed, e.g. made-up ones given dates of the
    /// test's choosing.
    /// </summary>
    public Task<TestingScenario> WithTransactions(string accountName, IEnumerable<TransactionDetails> details) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAccountAsync(context, accountName);
            context.BankTransactions.AddRange(details.Select(x => BankTransaction.Import(account, x)));
        });

    /// <summary>
    /// Records the transaction as a payment of the bill, as the user choosing it does.
    /// </summary>
    public Task<TestingScenario> WithBillPayment(string transactionName, string billName) =>
        PersistAsync(async context =>
        {
            Guid transactionId = Scenario.TransactionMappings[transactionName];
            Guid billId = Scenario.BillMappings[billName];

            BankTransaction transaction = await context.BankTransactions
                .SingleAsync(x => x.Id == transactionId, Scenario.CancellationToken);
            Bill bill = await context.Bills.SingleAsync(x => x.Id == billId, Scenario.CancellationToken);
            transaction.PayBill(bill, DateTime.UtcNow);
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

    /// <summary>
    /// Records the matcher's <paramref name="decision"/> on the transaction, <see cref="TransferDecision.Transfer"/>
    /// leaves it out of income and spending, <see cref="TransferDecision.NeedsReview"/> still counts it.
    /// </summary>
    public Task<TestingScenario> WithTransfer(string transactionName,
        TransferDecision decision = TransferDecision.Transfer) =>
        PersistAsync(async context =>
        {
            Guid transactionId = Scenario.TransactionMappings[transactionName];
            BankTransaction transaction = await context.BankTransactions
                .SingleAsync(x => x.Id == transactionId, Scenario.CancellationToken);

            transaction.DecideTransfer(decision == TransferDecision.Transfer
                ? TransferInfo.Matched(TransferReason.MirrorPair, null, Guid.NewGuid(), DateTime.UtcNow)
                : TransferInfo.ToReview(TransferReason.BankTransferLabel, null, null, DateTime.UtcNow));
        });

    private Task<BankAccount> LoadAccountAsync(Infrastructure.FinanceTrackerContext context, string accountName)
    {
        Guid accountId = Scenario.AccountMappings[accountName];
        return context.BankAccounts
            .AsNoTracking()
            .SingleAsync(x => x.Id == accountId, Scenario.CancellationToken);
    }
}
