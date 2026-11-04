using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class PayScenario : ScenarioBuilder
{
    public PayScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Gives the user a pay into the account on the last working day of the month, which came in on
    /// <paramref name="payDays"/>, so their months run from pay to pay.
    /// </summary>
    public Task<TestingScenario> WithPayOnTheLastWorkingDay(string username, string accountName,
        params DateOnly[] payDays)
    {
        DetectedPay pay = new(Scenario.AccountMappings[accountName], "ACMELTD", "ACME LTD", 2400m,
            PayPattern.LastWorkingDay, payDays);
        PaySchedule schedule = new(Scenario.UserMappings[username], pay);

        return PersistAsync(context => context.PaySchedules.Add(schedule));
    }
}
