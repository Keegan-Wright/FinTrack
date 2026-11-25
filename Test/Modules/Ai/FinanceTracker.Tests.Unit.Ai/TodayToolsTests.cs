using System.Text.Json;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Ai;

public class TodayToolsTests : AiToolTestBase
{
    [Test]
    [Arguments("en-GB", "2026-06-15", "GBP", "£")]
    [Arguments("en-US", "2026-06-14", "USD", "$")]
    public async Task GetToday_GivesTheDateWeekStartAndCurrencyOfTheCulture(string cultureName, string weekStart,
        string currency, string symbol)
    {
        // Arrange, the clock's day is a Monday, so the week starts that day in the UK and the day before in the US
        using CultureScope culture = new(cultureName);

        // Act
        JsonElement today = await CallAsync("get_today");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(today.GetProperty("today").GetString()).IsEqualTo("2026-06-15");
            await Assert.That(today.GetProperty("dayOfWeek").GetString()).IsEqualTo("Monday");
            await Assert.That(today.GetProperty("weekStart").GetString()).IsEqualTo(weekStart);
            await Assert.That(today.GetProperty("monthStart").GetString()).IsEqualTo("2026-06-01");
            await Assert.That(today.GetProperty("yearStart").GetString()).IsEqualTo("2026-01-01");
            await Assert.That(today.GetProperty("currency").GetString()).IsEqualTo(currency);
            await Assert.That(today.GetProperty("currencySymbol").GetString()).IsEqualTo(symbol);
        }
    }
}
