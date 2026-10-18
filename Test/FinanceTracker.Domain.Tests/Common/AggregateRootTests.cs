using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Tests.Common;

[Category("Domain")]
public class AggregateRootTests
{

    [Test]
    public void AggregateRoot_InvalidConstruction_Throws_ArgumentException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TestingAggregateRoot(Guid.Empty));
    }

    private sealed class TestingAggregateRoot : AggregateRoot
    {
        public TestingAggregateRoot(Guid id) : base(id)
        {

        }
    }
}
