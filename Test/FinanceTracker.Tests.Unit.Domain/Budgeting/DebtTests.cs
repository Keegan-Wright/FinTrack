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
}
