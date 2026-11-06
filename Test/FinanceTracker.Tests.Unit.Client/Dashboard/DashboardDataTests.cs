using System.Text.Json;
using FinanceTracker.Client.Components.Dashboard;

namespace FinanceTracker.Tests.Unit.Client.Dashboard;

/// <summary>
/// The dashboard carries what it loaded from the server's prerender to the browser as JSON (<c>[PersistentState]</c>),
/// so everything in it has to read back. Only a browser shows otherwise, the page fails to load there.
/// </summary>
public class DashboardDataTests
{
    [Test]
    public async Task Json_WithHouseholdIncome_ReadsBackAsItWas()
    {
        // Arrange
        DashboardData data = new()
        {
            Preset = DashboardRangePreset.ThisMonth,
            HouseholdIncome = HouseholdIncomeInRange.For(36_000m, DashboardRangePreset.ThisMonth,
                new DateOnly(2026, 11, 6))
        };

        // Act
        string json = JsonSerializer.Serialize(data, JsonSerializerOptions.Web);
        DashboardData? read = JsonSerializer.Deserialize<DashboardData>(json, JsonSerializerOptions.Web);

        // Assert
        await Assert.That(read!.HouseholdIncome).IsEqualTo(new HouseholdIncomeInRange(3_000m, "a month"));
    }
}
