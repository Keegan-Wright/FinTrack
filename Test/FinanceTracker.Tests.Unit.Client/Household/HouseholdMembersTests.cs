using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.HouseholdMember;

namespace FinanceTracker.Tests.Unit.Client.Household;

public class HouseholdMembersTests
{
    [Test]
    [Arguments(0, 0, "Income · payments not recognised")]
    [Arguments(1, 0, "Income · payments recognised by 1 name")]
    [Arguments(2, 1, "Income · payments recognised by 2 names and 1 account")]
    [Arguments(0, 2, "Income · payments recognised by 2 accounts")]
    public async Task RecognitionSummary_SaysHowTheyreRecognised(int names, int accounts, string expected)
    {
        // Arrange
        HouseholdMember member = new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Smith",
            PayeeNames = Enumerable.Range(0, names).Select(static x => $"Name {x}").ToList(),
            LinkedAccounts = Enumerable.Range(0, accounts)
                .Select(static x => new LinkedAccount { SortCode = "112233", Number = $"1234567{x}" })
                .ToList()
        };

        // Act
        string summary = HouseholdMembers.RecognitionSummary(member);

        // Assert
        await Assert.That(summary).IsEqualTo(expected);
    }
}
