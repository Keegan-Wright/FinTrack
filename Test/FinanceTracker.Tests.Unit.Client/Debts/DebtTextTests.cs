using FinanceTracker.Client.Components.Debts;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Debts;

public class DebtTextTests
{
    [Test]
    public async Task Forecast_PaidOffInMonths_SaysWhenAndHowManyPayments()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string many = DebtText.Forecast(Paying(18, new DateOnly(2028, 3, 1)));
        string one = DebtText.Forecast(Paying(1, new DateOnly(2026, 12, 1)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(many).IsEqualTo("Paid off by Mar 2028, 18 more payments");
            await Assert.That(one).IsEqualTo("Paid off by Dec 2026, 1 more payment");
        }
    }

    [Test]
    public async Task Forecast_NeverOrAlreadyPaidOff_SaysSo()
    {
        // Act
        string never = DebtText.Forecast(new DebtForecast());
        string done = DebtText.Forecast(Paying(0, new DateOnly(2026, 11, 2)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(never).IsEqualTo("Never paid off at this payment, it doesn't cover the interest");
            await Assert.That(done).IsEqualTo("Paid off");
        }
    }

    [Test]
    public async Task Terms_SaysTheLenderRatePaymentAndLinkedAccount()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string terms = DebtText.Terms(Debt(lender: "Car Finance Co", rate: 6.9m, accountId: Guid.NewGuid(),
            accountName: "Loan account"));
        string plain = DebtText.Terms(Debt());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(terms).IsEqualTo("Car Finance Co · 6.9% APR · £250.00 a month · Balance from Loan account");
            await Assert.That(plain).IsEqualTo("No interest · £250.00 a month");
        }
    }

    [Test]
    public async Task Goal_OnOrBehindTrack_SaysWhichAgainstTheGoal()
    {
        // Arrange
        using CultureScope culture = new();
        DateOnly goal = new(2028, 6, 1);

        // Act
        string? onTrack = DebtText.Goal(Debt(payOffGoal: goal, onTrack: true));
        string? behind = DebtText.Goal(Debt(payOffGoal: goal, onTrack: false));
        string? none = DebtText.Goal(Debt());
        string? paidOff = DebtText.Goal(Debt(owed: 0, payOffGoal: goal, onTrack: true));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(onTrack).IsEqualTo("On track for your goal of Jun 2028");
            await Assert.That(behind).IsEqualTo("Behind your goal of Jun 2028");
            await Assert.That(none).IsNull();
            await Assert.That(paidOff).IsNull();
        }
    }

    [Test]
    public async Task Compare_PayingMore_SaysHowMuchSoonerAndTheInterestSaved()
    {
        // Arrange
        using CultureScope culture = new();
        DebtForecast now = Paying(18, new DateOnly(2028, 3, 1), interest: 200m);
        DebtForecast more = Paying(11, new DateOnly(2027, 8, 1), interest: 80m);

        // Act
        string compared = DebtText.Compare(now, more, 300m);

        // Assert
        await Assert.That(compared)
            .IsEqualTo("At £300.00 a month it's paid off by Aug 2027, 7 months sooner, saving £120.00 interest");
    }

    [Test]
    public async Task Compare_NeverPaidOff_SaysSoEitherWay()
    {
        // Arrange
        using CultureScope culture = new();
        DebtForecast paying = Paying(40, new DateOnly(2030, 3, 1), interest: 900m);

        // Act
        string stillNever = DebtText.Compare(new DebtForecast(), new DebtForecast(), 50m);
        string nowItIs = DebtText.Compare(new DebtForecast(), paying, 300m);
        string noSooner = DebtText.Compare(paying, paying, 250m);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stillNever).IsEqualTo("At £50.00 a month it's never paid off, that doesn't cover the interest");
            await Assert.That(nowItIs).IsEqualTo("At £300.00 a month it's paid off by Mar 2030, when now it never is");
            await Assert.That(noSooner).IsEqualTo("At £250.00 a month it's paid off by Mar 2030, no sooner than now");
        }
    }

    [Test]
    [Arguments(TransferDecidedBy.User, null, "Chosen by you")]
    [Arguments(TransferDecidedBy.Rule, "Car finance", "By your rule Car finance")]
    public async Task Reason_SaysWhoLinkedThePayment(TransferDecidedBy decidedBy, string? ruleName, string expected)
    {
        // Act
        string reason = DebtText.Reason(decidedBy, ruleName);

        // Assert
        await Assert.That(reason).IsEqualTo(expected);
    }

    [Test]
    public async Task Progress_IsThePaidOffPercentAsAFraction()
    {
        // Act
        double progress = DebtText.Progress(Debt(paidOffPercent: 40m));

        // Assert
        await Assert.That(progress).IsEqualTo(0.4);
    }

    [Test]
    [Arguments(null, "Car loan")]
    [Arguments("Sam Taylor", "Car loan · Sam Taylor's")]
    public async Task PickerLabel_SaysWhoseItIsWhenSomeoneElses(string? sharedBy, string expected)
    {
        Debt debt = Debt();
        Debt shown = new()
        {
            Id = debt.Id, Name = debt.Name, Forecast = debt.Forecast, SharedBy = sharedBy
        };

        await Assert.That(DebtText.PickerLabel(shown)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(true, "Sam Taylor's · you can change it")]
    [Arguments(false, "Sam Taylor's · you can see it")]
    public async Task SharedText_SaysWhoseItIsAndWhatTheyCanDo(bool canChange, string expected) =>
        await Assert.That(DebtText.SharedText("Sam Taylor", canChange)).IsEqualTo(expected);

    internal static Debt Debt(string name = "Car loan", decimal owed = 900m, string? lender = null,
        decimal rate = 0m, Guid? accountId = null, string? accountName = null, DateOnly? payOffGoal = null,
        bool? onTrack = null, decimal paidOffPercent = 10m, DebtForecast? forecast = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Lender = lender,
        StartingBalance = 1000m,
        StartedOn = new DateOnly(2026, 1, 1),
        InterestRate = rate,
        MonthlyPayment = 250m,
        AccountId = accountId,
        AccountName = accountName,
        PayOffGoal = payOffGoal,
        Owed = owed,
        Paid = 1000m - owed,
        PaidOffPercent = paidOffPercent,
        Forecast = forecast ?? Paying(4, new DateOnly(2027, 3, 2)),
        OnTrack = onTrack
    };

    internal static DebtForecast Paying(int months, DateOnly paidOffOn, decimal interest = 0m) =>
        new() { Months = months, PaidOffOn = paidOffOn, Interest = interest };
}
