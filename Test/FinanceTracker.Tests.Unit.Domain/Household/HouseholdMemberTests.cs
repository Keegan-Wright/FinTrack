using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;

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
            _ = new HouseholdMember(Guid.NewGuid(), firstName, lastName, income));
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

        string newFirstName = "UpdatedFirstName";
        string newLastName = "UpdatedLastName";

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

        decimal newIncome = 500m;

        householdMember.ChangeIncome(newIncome);

        await Assert.That(householdMember.Income).IsEqualTo(newIncome);

    }

    [Test]
    public async Task HouseholdMember_ValidConstruction_SetsProperties()
    {
        var userId = Guid.NewGuid();

        var householdMember = new HouseholdMember(userId, "Jane", "Doe", 1500m);

        using (Assert.Multiple())
        {
            await Assert.That(householdMember.UserId).IsEqualTo(userId);
            await Assert.That(householdMember.FirstName).IsEqualTo("Jane");
            await Assert.That(householdMember.LastName).IsEqualTo("Doe");
            await Assert.That(householdMember.Income).IsEqualTo(1500m);
        }
    }

    [Test]
    public async Task HouseholdMember_NullIncome_IsValid()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", null);

        await Assert.That(householdMember.Income).IsNull();
    }

    [Test]
    public async Task HouseholdMember_ChangeIncome_Null_ClearsIncome()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        householdMember.ChangeIncome(null);

        await Assert.That(householdMember.Income).IsNull();
    }

    [Test]
    public async Task HouseholdMember_ChangeIncome_Zero_IsValid()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        householdMember.ChangeIncome(0m);

        await Assert.That(householdMember.Income).IsEqualTo(0m);
    }

    [Test]
    [Arguments(null, "User")]
    [Arguments("Test", null)]
    [Arguments(" ", "User")]
    [Arguments("Test", " ")]
    public void HouseholdMember_Rename_NullOrWhitespace_Throws_ArgumentException(string? firstName, string? lastName)
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        Assert.Throws<ArgumentException>(() =>
            householdMember.Rename(firstName!, lastName!));
    }

    [Test]
    public async Task HouseholdMember_Rename_Invalid_KeepsExistingName()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        Assert.Throws<ArgumentException>(() =>
            householdMember.Rename("Updated", ""));

        await Assert.That(householdMember.FirstName).IsEqualTo("Test");
    }

    [Test]
    public async Task HouseholdMember_AddPayeeName_TrimsAndIgnoresDuplicatesInAnyCase()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        householdMember.AddPayeeName("  J Smith ");
        householdMember.AddPayeeName("J Smith");

        await Assert.That(householdMember.PayeeNames).IsEquivalentTo(["J Smith"]);
    }

    [Test]
    public async Task HouseholdMember_ReplaceRecognition_ReplacesNamesAndAccounts()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);
        householdMember.AddPayeeName("Old Name");
        householdMember.LinkAccount(AccountIdentifier.Create("112233", "12345678", null)!);
        AccountIdentifier replacement = AccountIdentifier.Create("220040", "20589674", null)!;

        householdMember.ReplaceRecognition(["J Smith", "J Smith", "Jane Smith"], [replacement, replacement]);

        using (Assert.Multiple())
        {
            await Assert.That(householdMember.PayeeNames).IsEquivalentTo(["J Smith", "Jane Smith"]);
            await Assert.That(householdMember.LinkedAccounts).IsEquivalentTo([replacement]);
        }
    }

    [Test]
    public async Task HouseholdMember_ReplaceRecognition_BlankName_ThrowsAndKeepsWhatWasThere()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);
        householdMember.AddPayeeName("J Smith");

        Assert.Throws<ArgumentException>(() => householdMember.ReplaceRecognition(["Jane", " "], []));

        await Assert.That(householdMember.PayeeNames).IsEquivalentTo(["J Smith"]);
    }

    [Test]
    public async Task HouseholdMember_LinkAccount_IgnoresAnAlreadyLinkedAccount()
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Test", "User", 100m);

        householdMember.LinkAccount(AccountIdentifier.Create("11-22-33", "12345678", null)!);
        householdMember.LinkAccount(AccountIdentifier.Create("112233", "12345678", null)!);

        await Assert.That(householdMember.LinkedAccounts).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments("Jane Smith", null, true)]
    [Arguments("JANE SMITH", null, true)]
    [Arguments("  J Smith 01JAN26", null, true)]
    [Arguments("J Smith-Jones", null, true)]
    [Arguments("J SMITHSON LTD", null, false)]
    [Arguments("Tesco", null, false)]
    [Arguments(null, null, false)]
    [Arguments("J Smith", "112233 12345678", true)]
    [Arguments("Bills", "112233 12345678 01JAN26", true)]
    [Arguments("Bills", "112233 99999999", false)]
    public async Task HouseholdMember_Recognises_ByPayeeNameOrLinkedAccount(string? description, string? reference,
        bool recognised)
    {
        var householdMember = new HouseholdMember(Guid.NewGuid(), "Jane", "Smith", null);
        householdMember.AddPayeeName("Jane Smith");
        householdMember.AddPayeeName("J Smith");
        householdMember.LinkAccount(AccountIdentifier.Create("112233", "12345678", null)!);

        await Assert.That(householdMember.Recognises(description, reference)).IsEqualTo(recognised);
    }
}
