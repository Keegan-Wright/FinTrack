using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds the ASP.NET Core Data Protection key ring.
/// </summary>
public sealed class DataProtectionScenario : ScenarioBuilder
{
    public DataProtectionScenario(TestingScenario scenario) : base(scenario)
    {
    }

    public Task<TestingScenario> WithKey(string friendlyName, string xml) =>
        PersistAsync(context => context.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = friendlyName, Xml = xml }));
}
