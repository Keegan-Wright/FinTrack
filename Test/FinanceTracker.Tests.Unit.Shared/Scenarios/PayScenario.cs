using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds a user's pay, so their months run from pay to pay.
/// </summary>
public sealed class PayScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public PayScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Gives the user a pay into the account on the last working day of the month, which came in on
    /// <paramref name="payDays"/>, so their months run from pay to pay.
    /// </summary>
    /// <param name="username">The user, by their scenario name.</param>
    /// <param name="accountName">The account, by its scenario name.</param>
    /// <param name="payDays">The days the pay came in.</param>
    public Task<TestingScenario> WithPayOnTheLastWorkingDay(string username, string accountName,
        params DateOnly[] payDays)
    {
        DetectedPay pay = new(Scenario.AccountMappings[accountName], "ACMELTD", "ACME LTD", 2400m,
            PayPattern.LastWorkingDay, payDays);
        PaySchedule schedule = new(Scenario.UserMappings[username], pay);

        return PersistAsync(context => context.PaySchedules.Add(schedule));
    }
}
