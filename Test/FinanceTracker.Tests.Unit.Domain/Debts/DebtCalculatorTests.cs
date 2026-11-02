using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.Debts;

[Category("Domain")]
public class DebtCalculatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateTime At = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Progress_NoInterest_OwesTheStartLessItsPayments()
    {
        // Arrange
        Debt debt = Loan(1000m, 0m, 100m);
        BankTransaction[] payments =
        [
            Paying(debt, -100m, new DateTime(2026, 1, 15)), Paying(debt, -100m, new DateTime(2026, 2, 15))
        ];

        // Act
        DebtProgress progress = DebtCalculator.Progress(debt, null, payments, TimeZoneInfo.Utc, new DateOnly(2026, 3, 1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(progress.Owed).IsEqualTo(800m);
            await Assert.That(progress.Paid).IsEqualTo(200m);
            await Assert.That(progress.PaidOffPercent).IsEqualTo(20m);
        }
    }

    [Test]
    public async Task Progress_WithInterest_AddsATwelfthOfTheRateEachMonth()
    {
        // Arrange
        Debt debt = Loan(1200m, 12m, 100m);

        // Act
        DebtProgress progress = DebtCalculator.Progress(debt, null, [], TimeZoneInfo.Utc, new DateOnly(2026, 3, 1));

        // Assert: 1200 + 12, then 1212 + 12.12
        using (Assert.Multiple())
        {
            await Assert.That(progress.Owed).IsEqualTo(1224.12m);
            await Assert.That(progress.PaidOffPercent).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task Progress_InterestIsOnWhatsLeftAfterTheMonthsPayments()
    {
        // Arrange
        Debt debt = Loan(1000m, 12m, 100m);
        BankTransaction payment = Paying(debt, -100m, new DateTime(2026, 1, 10));

        // Act
        DebtProgress progress = DebtCalculator.Progress(debt, null, [payment], TimeZoneInfo.Utc, new DateOnly(2026, 2, 1));

        // Assert: 1000 - 100, plus 1% of 900
        await Assert.That(progress.Owed).IsEqualTo(909m);
    }

    [Test]
    public async Task Progress_PaymentsBeforeItStartedOrForAnotherDebt_DontCount()
    {
        // Arrange
        Debt debt = Loan(1000m, 0m, 100m);
        Debt other = Loan(500m, 0m, 50m);
        BankTransaction[] payments =
        [
            Paying(debt, -100m, new DateTime(2025, 12, 20)), Paying(other, -50m, new DateTime(2026, 1, 20)),
            Payment(-75m, new DateTime(2026, 1, 21))
        ];

        // Act
        DebtProgress progress = DebtCalculator.Progress(debt, null, payments, TimeZoneInfo.Utc, new DateOnly(2026, 2, 1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(progress.Owed).IsEqualTo(1000m);
            await Assert.That(progress.Paid).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task Progress_MoneyInLinkedToIt_AddsBack()
    {
        // Arrange
        Debt debt = Loan(1000m, 0m, 100m);
        BankTransaction[] payments =
        [
            Paying(debt, -200m, new DateTime(2026, 1, 10)), Paying(debt, 50m, new DateTime(2026, 1, 12))
        ];

        // Act
        DebtProgress progress = DebtCalculator.Progress(debt, null, payments, TimeZoneInfo.Utc, new DateOnly(2026, 1, 20));

        // Assert
        await Assert.That(progress.Owed).IsEqualTo(850m);
    }

    [Test]
    [Arguments(-450, 450)]
    [Arguments(20, 0)]
    public async Task Progress_LinkedToAnAccount_OwesWhatItsBalanceSays(double current, double owed)
    {
        // Arrange
        Debt debt = Loan(1000m, 19.9m, 100m);
        BankAccount card = new(UserId, Guid.NewGuid(), "card-1", "CREDIT", "Card", "GBP", BankAccountKind.Card);
        card.UpdateBalance(0m, (decimal)current, At, 1500m);
        debt.LinkAccount(card);

        // Act
        DebtProgress progress = DebtCalculator.Progress(debt, card, [Paying(debt, -900m, new DateTime(2026, 1, 10))],
            TimeZoneInfo.Utc, new DateOnly(2026, 2, 1));

        // Assert
        await Assert.That(progress.Owed).IsEqualTo((decimal)owed);
    }

    [Test]
    public async Task Progress_PayOffGoal_OnTrackWhenTheForecastIsntLater()
    {
        // Arrange
        Debt onTrack = Loan(1000m, 0m, 250m);
        onTrack.SetPayOffGoal(new DateOnly(2026, 5, 1));
        Debt behind = Loan(1000m, 0m, 250m);
        behind.SetPayOffGoal(new DateOnly(2026, 4, 30));

        // Act
        DebtProgress onTrackProgress = DebtCalculator.Progress(onTrack, null, [], TimeZoneInfo.Utc, Start);
        DebtProgress behindProgress = DebtCalculator.Progress(behind, null, [], TimeZoneInfo.Utc, Start);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(onTrackProgress.Forecast.PaidOffOn).IsEqualTo(new DateOnly(2026, 5, 1));
            await Assert.That(onTrackProgress.OnTrack).IsTrue();
            await Assert.That(behindProgress.OnTrack).IsFalse();
        }
    }

    [Test]
    public async Task Forecast_NoInterest_PaysOffInWholeMonths()
    {
        // Act
        DebtForecast forecast = DebtCalculator.Forecast(1000m, 0m, 250m, new DateOnly(2026, 3, 1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(forecast.PaidOffOn).IsEqualTo(new DateOnly(2026, 7, 1));
            await Assert.That(forecast.Months).IsEqualTo(4);
            await Assert.That(forecast.Interest).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task Forecast_WithInterest_AddsItOnWhatsLeftAfterEachPayment()
    {
        // Act
        DebtForecast forecast = DebtCalculator.Forecast(1000m, 12m, 500m, new DateOnly(2026, 3, 1));

        // Assert: 500 left plus 5, then 5 left plus 0.05, then paid off
        using (Assert.Multiple())
        {
            await Assert.That(forecast.Months).IsEqualTo(3);
            await Assert.That(forecast.Interest).IsEqualTo(5.05m);
        }
    }

    [Test]
    public async Task Forecast_PayingMore_PaysOffSooner()
    {
        // Act
        DebtForecast usual = DebtCalculator.Forecast(5000m, 9.9m, 200m, Start);
        DebtForecast more = DebtCalculator.Forecast(5000m, 9.9m, 250m, Start);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(more.Months!.Value).IsLessThan(usual.Months!.Value);
            await Assert.That(more.Interest!.Value).IsLessThan(usual.Interest!.Value);
        }
    }

    [Test]
    public async Task Forecast_PaymentDoesntCoverTheInterest_IsNeverPaidOff()
    {
        // Act: 2% a month on what's left after 150 is more than 150
        DebtForecast forecast = DebtCalculator.Forecast(10000m, 24m, 150m, Start);

        // Assert
        await Assert.That(forecast.IsNeverPaidOff).IsTrue();
    }

    [Test]
    public async Task Forecast_NothingOwed_IsPaidOffToday()
    {
        // Act
        DebtForecast forecast = DebtCalculator.Forecast(0m, 9.9m, 200m, Start);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(forecast.PaidOffOn).IsEqualTo(Start);
            await Assert.That(forecast.Months).IsEqualTo(0);
        }
    }

    [Test]
    public async Task History_OneBalanceAtTheStartEachMonthEndAndToday()
    {
        // Arrange
        Debt debt = Loan(1000m, 0m, 100m);
        BankTransaction payment = Paying(debt, -100m, new DateTime(2026, 1, 15));

        // Act
        IReadOnlyList<DebtBalance> history =
            DebtCalculator.History(debt, [payment], TimeZoneInfo.Utc, new DateOnly(2026, 2, 10));

        // Assert
        await Assert.That(history).IsEquivalentTo([
            new DebtBalance(Start, 1000m), new DebtBalance(new DateOnly(2026, 2, 1), 900m),
            new DebtBalance(new DateOnly(2026, 2, 10), 900m)
        ]);
    }

    [Test]
    public async Task OwedOn_BeforeItStarted_IsNothing() =>
        await Assert.That(DebtCalculator.OwedOn(Loan(1000m, 0m, 100m), [], TimeZoneInfo.Utc, new DateOnly(2025, 12, 31)))
            .IsEqualTo(0m);

    [Test]
    public async Task OwedOn_LinkedToAnAccount_Throws()
    {
        // Arrange
        Debt debt = Loan(1000m, 0m, 100m);
        debt.LinkAccount(Account);

        // Act & Assert
        await Assert.That(() => DebtCalculator.OwedOn(debt, [], TimeZoneInfo.Utc, Start))
            .Throws<InvalidOperationException>();
    }

    private static Debt Loan(decimal startingBalance, decimal interestRate, decimal monthlyPayment) =>
        new(UserId, "Car loan", null, startingBalance, Start, interestRate, monthlyPayment);

    private static BankTransaction Paying(Debt debt, decimal amount, DateTime occurredAt)
    {
        BankTransaction transaction = Payment(amount, occurredAt);
        transaction.PayDebt(debt, At);
        return transaction;
    }

    private static BankTransaction Payment(decimal amount, DateTime occurredAt) =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), "CAR FINANCE",
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "PURCHASE", amount, "GBP",
            DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc), false, [], TransactionMetadata.None, false));
}
