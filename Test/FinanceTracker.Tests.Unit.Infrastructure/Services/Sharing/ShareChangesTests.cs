using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Sharing;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

public class ShareChangesTests
{
    private static readonly Guid Joint = Guid.NewGuid();
    private static readonly Guid Savings = Guid.NewGuid();
    private static readonly Guid Groceries = Guid.NewGuid();
    private static readonly Guid Rent = Guid.NewGuid();

    [Test]
    public async Task Describe_NothingChanged_IsNull()
    {
        Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode> shared = new()
        {
            [(ShareItemType.BankAccount, Joint)] = ShareMode.ReadOnly
        };

        await Assert.That(ShareChanges.Describe(shared, new(shared))).IsNull();
    }

    [Test]
    public async Task Describe_SaysWhatWasSharedStoppedAndChanged_CountingEachKind()
    {
        // Arrange
        Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode> before = new()
        {
            [(ShareItemType.BankAccount, Joint)] = ShareMode.ReadOnly,
            [(ShareItemType.Bill, Rent)] = ShareMode.AllowChanges
        };
        Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode> after = new()
        {
            [(ShareItemType.BankAccount, Joint)] = ShareMode.AllowChanges,
            [(ShareItemType.BankAccount, Savings)] = ShareMode.ReadOnly,
            [(ShareItemType.Classification, Groceries)] = ShareMode.ReadOnly
        };

        // Act
        string? described = ShareChanges.Describe(before, after);

        // Assert
        await Assert.That(described).IsEqualTo(
            "They shared 1 account and 1 tag with you. They stopped sharing 1 bill. You can change 1 account now.");
    }

    [Test]
    public async Task Describe_SeveralOfAKind_CountsThem()
    {
        Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode> before = new()
        {
            [(ShareItemType.SavingsGoal, Joint)] = ShareMode.AllowChanges,
            [(ShareItemType.SavingsGoal, Savings)] = ShareMode.AllowChanges
        };
        Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode> after = new()
        {
            [(ShareItemType.SavingsGoal, Joint)] = ShareMode.ReadOnly,
            [(ShareItemType.SavingsGoal, Savings)] = ShareMode.ReadOnly
        };

        await Assert.That(ShareChanges.Describe(before, after)).IsEqualTo("You can only see 2 savings goals now.");
    }
}
