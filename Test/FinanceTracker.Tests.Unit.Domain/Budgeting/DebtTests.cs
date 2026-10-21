using FinanceTracker.Domain.Budgeting;

namespace FinanceTracker.Tests.Unit.Domain.Budgeting;

[Category("Domain")]
public class DebtTests
{

    [Test]
    [Arguments("", 100)]
    [Arguments("Test", -1)]
    public void Debt_InvalidConstruction_Throws_ArgumentException(string name, decimal monthlyPayment)
    {
        Assert.Throws<ArgumentException>(() =>
            new Debt(Guid.NewGuid(), name, monthlyPayment,null, null));
    }

    [Test]
    public void Debt_ChangeMonthlyPayment_Negative_Throws_ArgumentException()
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);

        Assert.Throws<ArgumentException>(() =>
            debt.ChangeMonthlyPayment(-1));
    }

    [Test]
    public async Task Debt_ChangeMonthlyPayment_ValidDecimal_DoesNotThrow()
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);

        var newMonthlyPayment = 300m;

        debt.ChangeMonthlyPayment(newMonthlyPayment);

        await Assert.That(debt.MonthlyPayment).IsEqualTo(newMonthlyPayment);
    }

    [Test]
    public void Debt_Rename_EmptyString_Throws_ArgumentException()
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);

        Assert.Throws<ArgumentException>(() =>
            debt.Rename(""));
    }

    [Test]
    public async Task Debt_Rename_ValidString_DoesNotThrow()
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);
        var newName = "Renamed";

        debt.Rename(newName);

        await Assert.That(debt.Name).IsEqualTo(newName);
    }

    [Test]
    public async Task Debt_ValidConstruction_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var finalPayment = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var payOffGoal = new DateTime(2028, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var debt = new Debt(userId, "Car Loan", 250m, finalPayment, payOffGoal);

        using (Assert.Multiple())
        {
            await Assert.That(debt.UserId).IsEqualTo(userId);
            await Assert.That(debt.Name).IsEqualTo("Car Loan");
            await Assert.That(debt.MonthlyPayment).IsEqualTo(250m);
            await Assert.That(debt.FinalPaymentDate).IsEqualTo(finalPayment);
            await Assert.That(debt.PayOffGoal).IsEqualTo(payOffGoal);
        }
    }

    [Test]
    public void Debt_ZeroMonthlyPayment_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Debt(Guid.NewGuid(), "Test", 0m, null, null));
    }

    [Test]
    public void Debt_ChangeMonthlyPayment_Zero_Throws_ArgumentOutOfRangeException()
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            debt.ChangeMonthlyPayment(0m));
    }

    [Test]
    [Arguments(null)]
    [Arguments(" ")]
    public void Debt_Rename_NullOrWhitespace_Throws_ArgumentException(string? name)
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);

        Assert.Throws<ArgumentException>(() =>
            debt.Rename(name!));
    }

    [Test]
    public async Task Debt_ChangeFinalPaymentDate_SetsAndClearsDate()
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);
        var finalPayment = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        debt.ChangeFinalPaymentDate(finalPayment);
        var afterSet = debt.FinalPaymentDate;
        debt.ChangeFinalPaymentDate(null);

        using (Assert.Multiple())
        {
            await Assert.That(afterSet).IsEqualTo(finalPayment);
            await Assert.That(debt.FinalPaymentDate).IsNull();
        }
    }

    [Test]
    public async Task Debt_SetPayOffGoal_SetsAndClearsGoal()
    {
        var debt = new Debt(Guid.NewGuid(), "Test", 100m, null, null);
        var payOffGoal = new DateTime(2028, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        debt.SetPayOffGoal(payOffGoal);
        var afterSet = debt.PayOffGoal;
        debt.SetPayOffGoal(null);

        using (Assert.Multiple())
        {
            await Assert.That(afterSet).IsEqualTo(payOffGoal);
            await Assert.That(debt.PayOffGoal).IsNull();
        }
    }
}
