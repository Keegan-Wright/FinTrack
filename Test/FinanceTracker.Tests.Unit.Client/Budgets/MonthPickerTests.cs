using Bunit;
using FinanceTracker.Client.Components.Budgets;

namespace FinanceTracker.Tests.Unit.Client.Budgets;

public class MonthPickerTests : ComponentTestBase
{
    private static readonly DateOnly March = new(2026, 3, 1);

    [Test]
    public async Task Previous_AsksForTheMonthBefore()
    {
        // Arrange
        DateOnly? asked = null;
        IRenderedComponent<MonthPicker> cut = Render(March, month => asked = month);

        // Act
        cut.Find("button[aria-label='Previous month']").Click();

        // Assert
        await Assert.That(asked).IsEqualTo(new DateOnly(2026, 2, 1));
    }

    [Test]
    public async Task Next_AtTheCurrentMonth_IsDisabled()
    {
        // Act
        IRenderedComponent<MonthPicker> cut = Render(March, _ => { });

        // Assert
        await Assert.That(cut.Find("button[aria-label='Next month']").HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task Render_NamesTheMonth()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<MonthPicker> cut = Render(new DateOnly(2026, 1, 1), _ => { });

        // Assert
        await Assert.That(cut.Markup).Contains("January 2026");
    }

    private IRenderedComponent<MonthPicker> Render(DateOnly month, Action<DateOnly> monthChanged) =>
        Render<MonthPicker>(p => p
            .Add(x => x.Month, month)
            .Add(x => x.Latest, March)
            .Add(x => x.MonthChanged, monthChanged));
}
