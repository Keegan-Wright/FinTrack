using FinanceTracker.Domain.Common;

namespace FinanceTracker.Tests.Unit.Domain.Common;

[Category("Domain")]
public class AggregateRootTests
{

    [Test]
    public void AggregateRoot_InvalidConstruction_Throws_ArgumentException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new TestingAggregateRoot(Guid.Empty));
    }

    [Test]
    public async Task AggregateRoot_ValidConstruction_SetsOwnerAndIdentity()
    {
        var userId = Guid.NewGuid();

        var aggregate = new TestingAggregateRoot(userId);

        using (Assert.Multiple())
        {
            await Assert.That(aggregate.UserId).IsEqualTo(userId);
            await Assert.That(aggregate.Id).IsNotEqualTo(Guid.Empty);
            await Assert.That(aggregate.RowVersion).IsEqualTo(0u);
        }
    }

    [Test]
    public async Task AggregateRoot_IsUserOwned()
    {
        IUserOwned aggregate = new TestingAggregateRoot(Guid.NewGuid());

        await Assert.That(aggregate.UserId).IsNotEqualTo(Guid.Empty);
    }

    private sealed class TestingAggregateRoot : AggregateRoot
    {
        public TestingAggregateRoot(Guid id) : base(id)
        {

        }
    }
}
