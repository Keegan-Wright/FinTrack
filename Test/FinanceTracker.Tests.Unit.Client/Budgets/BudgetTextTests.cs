using FinanceTracker.Client.Components.Budgets;
using FinanceTracker.Contracts.Budgets;

namespace FinanceTracker.Tests.Unit.Client.Budgets;

public class BudgetTextTests
{
    [Test]
    [Arguments(120, 400, "£120.00 of £400.00", "£280.00 left")]
    [Arguments(430, 400, "£430.00 of £400.00", "£30.00 over")]
    public async Task SpentAndRemaining_SayWhereItStands(int spent, int available, string spentOf, string remaining)
    {
        // Arrange
        using CultureScope culture = new();
        BudgetStatus status = Status(spent, available);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(BudgetText.SpentOf(status)).IsEqualTo(spentOf);
            await Assert.That(BudgetText.Remaining(status)).IsEqualTo(remaining);
        }
    }

    [Test]
    [Arguments(100, 120, "On pace")]
    [Arguments(160, 120, "£40.00 ahead of pace")]
    public async Task Pace_ComparesWithASteadyPace(int spent, int expected, string text)
    {
        // Arrange
        using CultureScope culture = new();

        // Act & Assert
        await Assert.That(BudgetText.Pace(Status(spent, 400, expected))).IsEqualTo(text);
    }

    [Test]
    [Arguments(50, "Includes £50.00 carried over")]
    [Arguments(-30, "£30.00 less for last month's overspend")]
    [Arguments(0, null)]
    public async Task CarriedIn_SaysWhatCameFromLastMonth(int carriedIn, string? text)
    {
        // Arrange
        using CultureScope culture = new();

        // Act & Assert
        await Assert.That(BudgetText.CarriedIn(Status(0, 400, carriedIn: carriedIn))).IsEqualTo(text);
    }

    [Test]
    [Arguments(100, 400, 0.25)]
    [Arguments(500, 400, 1.0)]
    [Arguments(-20, 400, 0.0)]
    [Arguments(10, 0, 1.0)]
    [Arguments(0, 0, 0.0)]
    public async Task Progress_IsHowMuchIsUsedUp(int spent, int available, double progress) =>
        await Assert.That(BudgetText.Progress(Status(spent, available))).IsEqualTo(progress);

    private static BudgetStatus Status(decimal spent, decimal available, decimal expected = 0, decimal carriedIn = 0) =>
        new()
        {
            Name = "Groceries",
            Limit = available - carriedIn,
            CarriedIn = carriedIn,
            Available = available,
            Spent = spent,
            Remaining = available - spent,
            ExpectedByNow = expected,
            OnPace = spent <= expected
        };
}
