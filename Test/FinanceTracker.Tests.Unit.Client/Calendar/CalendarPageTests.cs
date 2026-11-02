using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using CalendarPage = FinanceTracker.Client.Pages.Calendar;

namespace FinanceTracker.Tests.Unit.Client.Calendar;

public class CalendarPageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly ICalendarService _calendarService = Substitute.For<ICalendarService>();
    private readonly ApplicationState _applicationState = new();

    public CalendarPageTests()
    {
        AddBunitPersistentComponentState();
        ISavingsGoalsService savingsGoalsService = Substitute.For<ISavingsGoalsService>();
        savingsGoalsService.GetSavingsGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<SavingsGoal>());

        Services.AddSingleton(_calendarService);
        Services.AddSingleton(savingsGoalsService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
    }

    [Test]
    public async Task Render_TodayWithAnEventAndADebt_ListsThemInTheDayPanel()
    {
        // Arrange
        using CultureScope culture = new();
        Month(new CalendarItems
        {
            Date = new DateTime(2026, 3, 14),
            Events = [CalendarTextTests.Event(target: 100m, goalName: "Gifts", saved: 40m, notes: "Book a table")],
            Debts = [new CalendarDebtItem { DebtId = Guid.NewGuid(), Name = "Car loan", Kind = CalendarDebtDate.PaidOff }]
        });

        // Act
        IRenderedComponent<CalendarPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Birthday");
            await Assert.That(cut.Markup).Contains("£40.00 of £100.00 saved in Gifts");
            await Assert.That(cut.Markup).Contains("Book a table");
            await Assert.That(cut.Markup).Contains("Car loan");
            await Assert.That(cut.Markup).Contains("Forecast to be paid off");
            await Assert.That(cut.FindAll("a[href^='Debts/']")).Count().IsEqualTo(1);
            await Assert.That(cut.FindAll("[aria-label='Actions for Birthday']")).Count().IsEqualTo(1);
            await Assert.That(cut.FindAll("[aria-label='1 event']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task Render_QuietDay_StillOffersAnEvent()
    {
        // Arrange
        Month();

        // Act
        IRenderedComponent<CalendarPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("A quiet day");
            await Assert.That(cut.FindAll("button").Count(x => x.TextContent.Trim() == "Add event")).IsEqualTo(1);
        }
    }

    /// <summary>
    /// March 2026, with <paramref name="days"/> in place of the empty days they're on.
    /// </summary>
    private void Month(params CalendarItems[] days)
    {
        List<CalendarItems> month = Enumerable.Range(1, 31)
            .Select(day => days.FirstOrDefault(x => x.Date.Day == day) ??
                           new CalendarItems { Date = new DateTime(2026, 3, day) })
            .ToList();
        _calendarService.GetMonthItemsAsync(3, 2026, Arg.Any<CancellationToken>())
            .Returns(_ => month.ToAsyncEnumerable());
    }

    private IRenderedComponent<CalendarPage> RenderPage() =>
        Render<CalendarPage>(p => p.AddCascadingValue(_applicationState));
}
