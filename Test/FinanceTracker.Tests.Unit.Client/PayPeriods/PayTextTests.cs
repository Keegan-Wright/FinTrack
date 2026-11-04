using FinanceTracker.Client.Components.PayPeriods;
using FinanceTracker.Contracts.PayPeriods;

namespace FinanceTracker.Tests.Unit.Client.PayPeriods;

public class PayTextTests
{
    [Test]
    [Arguments(1, "1st")]
    [Arguments(2, "2nd")]
    [Arguments(3, "3rd")]
    [Arguments(4, "4th")]
    [Arguments(11, "11th")]
    [Arguments(12, "12th")]
    [Arguments(13, "13th")]
    [Arguments(21, "21st")]
    [Arguments(22, "22nd")]
    [Arguments(31, "31st")]
    public async Task Ordinal_AddsTheSuffix(int day, string expected) =>
        await Assert.That(PayText.Ordinal(day)).IsEqualTo(expected);

    [Test]
    public async Task When_DayOfMonth_SaysWhichDay()
    {
        // Arrange
        Income income = YourPays.Pay("ACME LTD", 2400m);
        Income midMonth = new()
        {
            AccountId = income.AccountId, AccountName = income.AccountName, Key = income.Key, Name = income.Name,
            Amount = income.Amount, Kind = PayDayKind.DayOfMonth, Day = 15, LastPaidOn = income.LastPaidOn
        };

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(PayText.When(income)).IsEqualTo("the last working day of the month");
            await Assert.That(PayText.When(midMonth)).IsEqualTo("the 15th of each month");
        }
    }

    [Test]
    public async Task Period_InOneYear_LeavesTheYearOut()
    {
        // Arrange
        using CultureScope culture = new();

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(PayText.Period(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 29)))
                .IsEqualTo("30 Oct – 29 Nov");
            await Assert.That(PayText.Period(new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 28)))
                .IsEqualTo("31 Dec 2026 – 28 Jan 2027");
        }
    }
}
