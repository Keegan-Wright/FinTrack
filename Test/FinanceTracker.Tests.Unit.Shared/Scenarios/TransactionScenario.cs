using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds transactions and the decisions on them, mapped by name in <see cref="TestingScenario.TransactionMappings"/>.
/// </summary>
public sealed class TransactionScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public TransactionScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Imports a transaction onto the account.
    /// </summary>
    /// <param name="transactionName">The name to map the transaction by.</param>
    /// <param name="accountName">The account, by its scenario name.</param>
    /// <param name="details">
    /// Generated when omitted, use <c>new TransactionDetailsFaker().Generate() with { ... }</c> to set specific values.
    /// </param>
    public Task<TestingScenario> WithTransaction(string transactionName, string accountName,
        TransactionDetails? details = null) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAccountAsync(context, accountName);
            BankTransaction transaction =
                BankTransaction.Import(account, details ?? new TransactionDetailsFaker().Generate());

            context.BankTransactions.Add(transaction);
            Scenario.TransactionMappings.Add(transactionName, transaction.Id);
        });

    /// <summary>
    /// Imports <paramref name="count"/> generated transactions onto the account.
    /// </summary>
    /// <param name="accountName">The account, by its scenario name.</param>
    /// <param name="count">How many to import.</param>
    public Task<TestingScenario> WithTransactions(string accountName, int count) =>
        WithTransactions(accountName, new TransactionDetailsFaker().Generate(count));

    /// <summary>
    /// Imports each of <paramref name="details"/> into the account, unnamed, e.g. made-up ones given dates of the
    /// test's choosing.
    /// </summary>
    /// <param name="accountName">The account, by its scenario name.</param>
    /// <param name="details">What the provider reports for each.</param>
    public Task<TestingScenario> WithTransactions(string accountName, IEnumerable<TransactionDetails> details) =>
        PersistAsync(async context =>
        {
            BankAccount account = await LoadAccountAsync(context, accountName);
            context.BankTransactions.AddRange(details.Select(x => BankTransaction.Import(account, x)));
        });

    /// <summary>
    /// Records the transaction as a payment of the bill, as the user choosing it does.
    /// </summary>
    /// <param name="transactionName">The transaction, by its scenario name.</param>
    /// <param name="billName">The bill, by its scenario name.</param>
    public Task<TestingScenario> WithBillPayment(string transactionName, string billName) =>
        PersistAsync(async context =>
        {
            Guid transactionId = Scenario.TransactionMappings[transactionName];
            Guid billId = Scenario.BillMappings[billName];

            BankTransaction transaction = await context.BankTransactions
                .SingleAsync(x => x.Id == transactionId, Scenario.CancellationToken);
            Bill bill = await context.Bills.SingleAsync(x => x.Id == billId, Scenario.CancellationToken);
            transaction.PayBill(bill, transaction.UserId, DateTime.UtcNow);
        });

    /// <summary>
    /// Tags the transaction, as its owner does.
    /// </summary>
    /// <param name="transactionName">The transaction, by its scenario name.</param>
    /// <param name="classificationName">The tag, by its scenario name.</param>
    public Task<TestingScenario> WithCustomClassification(string transactionName, string classificationName) =>
        PersistAsync(async context =>
        {
            Guid transactionId = Scenario.TransactionMappings[transactionName];
            Guid classificationId = Scenario.ClassificationMappings[classificationName];

            BankTransaction transaction = await context.BankTransactions
                .SingleAsync(x => x.Id == transactionId, Scenario.CancellationToken);
            CustomClassification classification = await context.CustomClassifications
                .SingleAsync(x => x.Id == classificationId, Scenario.CancellationToken);

            transaction.Classify(classification, transaction.UserId);
        });

    /// <summary>
    /// Records the matcher's <paramref name="decision"/> on the transaction, <see cref="TransferDecision.Transfer"/>
    /// leaves it out of income and spending, <see cref="TransferDecision.NeedsReview"/> still counts it.
    /// </summary>
    /// <param name="transactionName">The transaction, by its scenario name.</param>
    /// <param name="decision">What the matcher decided.</param>
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

    /// <summary>
    /// Loads the account a transaction is imported onto, untracked.
    /// </summary>
    /// <param name="context">The context to load it in.</param>
    /// <param name="accountName">The account, by its scenario name.</param>
    private Task<BankAccount> LoadAccountAsync(Infrastructure.FinanceTrackerContext context, string accountName)
    {
        Guid accountId = Scenario.AccountMappings[accountName];
        return context.BankAccounts
            .AsNoTracking()
            .SingleAsync(x => x.Id == accountId, Scenario.CancellationToken);
    }
}
