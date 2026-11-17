using FinanceTracker.Domain.Bills;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds bills and bill suggestions, mapped by name in <see cref="TestingScenario.BillMappings"/> and <see
/// cref="TestingScenario.BillSuggestionMappings"/>.
/// </summary>
public sealed class BillScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public BillScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a bill paid from the account.
    /// </summary>
    /// <param name="billName">The name to map the bill by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
    /// <param name="accountName">The account, by its scenario name.</param>
    /// <param name="build">Builds the bill from the user and account ids, generated when omitted.</param>
    public Task<TestingScenario> WithBill(string billName, string username, string accountName,
        Func<Guid, Guid, Bill>? build = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Guid accountId = Scenario.AccountMappings[accountName];
        Bill bill = build is null ? new BillFaker(userId, accountId).Generate() : build(userId, accountId);

        Scenario.BillMappings.Add(billName, bill.Id);
        return PersistAsync(context => context.Bills.Add(bill));
    }

    /// <summary>
    /// Adds a subscription suggested from the payments on the account.
    /// </summary>
    /// <param name="suggestionName">The name to map the suggestion by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
    /// <param name="accountName">The account, by its scenario name.</param>
    /// <param name="name">The payee as the bank names them.</param>
    /// <param name="amount">The latest payment's amount.</param>
    /// <param name="firstPaidOn">The day of the first payment.</param>
    /// <param name="lastPaidOn">The day of the latest payment.</param>
    /// <param name="paymentNames">The transactions behind it, by their scenario names.</param>
    public Task<TestingScenario> WithSuggestion(string suggestionName, string username, string accountName,
        string name, decimal amount, DateOnly firstPaidOn, DateOnly lastPaidOn, params string[] paymentNames)
    {
        Guid userId = Scenario.UserMappings[username];
        DetectedSubscription detected = new(Scenario.AccountMappings[accountName],
            new string(name.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray()), name, BillFrequency.Monthly,
            amount, firstPaidOn, lastPaidOn, paymentNames.Select(x => Scenario.TransactionMappings[x]).ToList());
        BillSuggestion suggestion = new(userId, detected);

        Scenario.BillSuggestionMappings.Add(suggestionName, suggestion.Id);
        return PersistAsync(context => context.BillSuggestions.Add(suggestion));
    }
}
