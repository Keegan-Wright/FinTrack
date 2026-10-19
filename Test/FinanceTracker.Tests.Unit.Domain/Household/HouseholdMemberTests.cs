using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Household;

namespace FinanceTracker.Tests.Unit.Domain.Household;

[Category("Domain")]
public class HouseholdMemberTests
{

    [Test]
    [Arguments("", "User", 100)]
    [Arguments("Test", "", 100)]
    [Arguments("Test", "User", -1)]
    public void HouseholdMember_InvalidConstruction_Throws_ArgumentException(string firstName, string lastName,
        decimal income)
    {
        Assert.Throws<ArgumentException>(() =>
            new HouseholdMember(Guid.NewGuid(), firstName, lastName, income));
    }

    [Test]
    [Arguments("Test", "")]
    [Arguments("", "User")]
    public void HouseholdMember_Rename_EmptyString_Throws_ArgumentException(string firstName, string lastName)
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(),"Test", "User", 100m);

        Assert.Throws<ArgumentException>(() =>
            householdMember.Rename(firstName, lastName));
    }

    [Test]
    public async Task HouseholdMember_Rename_ValidString_DoesNotThrow()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        var newFirstName = "UpdatedFirstName";
        var newLastName = "UpdatedLastName";

        householdMember.Rename(newFirstName, newLastName);

        using (Assert.Multiple())
        {
            await Assert.That(householdMember.FirstName).IsEqualTo(newFirstName);
            await Assert.That(householdMember.LastName).IsEqualTo(newLastName);
        }
    }


    [Test]
    public void HouseholdMember_ChangeIncome_InvalidDecimal_Throws_ArgumentException()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(),"Test", "User", 100m);

        Assert.Throws<ArgumentException>(() =>
            householdMember.ChangeIncome(-1m));
    }

    [Test]
    public async Task HouseholdMember_ChnageIncome_ValidDecimal_DoesNotThrow()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        var newIncome = 500m;

        householdMember.ChangeIncome(newIncome);

        await Assert.That(householdMember.Income).IsEqualTo(newIncome);

    }

}
