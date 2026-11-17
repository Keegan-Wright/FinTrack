using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds the ASP.NET Core Data Protection key ring.
/// </summary>
/// <summary>
/// Seeds the keys ASP.NET Core's data protection stores in the database.
/// </summary>
public sealed class DataProtectionScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public DataProtectionScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a data protection key.
    /// </summary>
    /// <param name="friendlyName">The key's name.</param>
    /// <param name="xml">The key as data protection stores it.</param>
    public Task<TestingScenario> WithKey(string friendlyName, string xml) =>
        PersistAsync(context =>
            context.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = friendlyName, Xml = xml }));
}
