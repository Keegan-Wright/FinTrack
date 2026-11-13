using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Classifications;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class TransactionChoicesTests
{
    private static readonly Classification Mine = new() { ClassificationId = Guid.NewGuid(), Tag = "Food" };

    private static readonly Classification Sams = new()
    {
        ClassificationId = Guid.NewGuid(), Tag = "Groceries", SharedBy = "Sam Taylor"
    };

    private static readonly Classification Chriss = new()
    {
        ClassificationId = Guid.NewGuid(), Tag = "Gifts", SharedBy = "Chris Jones"
    };

    [Test]
    public async Task For_SomeoneElsesTransaction_OffersTheUsersAndTheOwnersOnly()
    {
        List<Classification> choices = TransactionChoices.For([Mine, Sams, Chriss], static x => x.SharedBy,
            TransactionListTests.Transaction("TESCO", -10m, sharedBy: "Sam Taylor"));

        await Assert.That(choices).IsEquivalentTo([Mine, Sams]);
    }

    [Test]
    public async Task For_TheirOwnTransaction_OffersEverything()
    {
        List<Classification> choices = TransactionChoices.For([Mine, Sams, Chriss], static x => x.SharedBy,
            TransactionListTests.Transaction("TESCO", -10m));

        await Assert.That(choices).IsEquivalentTo([Mine, Sams, Chriss]);
    }
}
