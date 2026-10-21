using FinanceTracker.Domain.Common;

namespace FinanceTracker.Tests.Unit.Domain.Common;

[Category("Domain")]
public class EntityTests
{

    [Test]
    public async Task Entity_Construction_AssignsUniqueVersion7Identity()
    {
        var first = new TestingEntity();
        var second = new TestingEntity();

        using (Assert.Multiple())
        {
            await Assert.That(first.Id).IsNotEqualTo(Guid.Empty);
            await Assert.That(first.Id).IsNotEqualTo(second.Id);
            await Assert.That(first.Id.Version).IsEqualTo(7);
        }
    }

    [Test]
    public async Task Entity_Construction_AuditFieldsUnset()
    {
        IAuditable entity = new TestingEntity();

        using (Assert.Multiple())
        {
            await Assert.That(entity.Created).IsEqualTo(default(DateTime));
            await Assert.That(entity.Updated).IsNull();
        }
    }

    [Test]
    public async Task Entity_SameId_AreEqual()
    {
        var id = Guid.NewGuid();
        var first = new TestingEntity { Id = id };
        var second = new TestingEntity { Id = id };

        using (Assert.Multiple())
        {
            await Assert.That(first.Equals(second)).IsTrue();
            await Assert.That(first.Equals((object)second)).IsTrue();
            await Assert.That(first == second).IsTrue();
            await Assert.That(first != second).IsFalse();
            await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
        }
    }

    [Test]
    public async Task Entity_DifferentId_AreNotEqual()
    {
        var first = new TestingEntity();
        var second = new TestingEntity();

        using (Assert.Multiple())
        {
            await Assert.That(first.Equals(second)).IsFalse();
            await Assert.That(first == second).IsFalse();
            await Assert.That(first != second).IsTrue();
        }
    }

    [Test]
    public async Task Entity_SameIdDifferentType_AreNotEqual()
    {
        var id = Guid.NewGuid();
        Entity first = new TestingEntity { Id = id };
        Entity second = new OtherTestingEntity { Id = id };

        using (Assert.Multiple())
        {
            await Assert.That(first.Equals(second)).IsFalse();
            await Assert.That(first == second).IsFalse();
        }
    }

    [Test]
    public async Task Entity_ComparedToNull_IsNotEqual()
    {
        TestingEntity entity = new();
        TestingEntity? nullEntity = null;

        using (Assert.Multiple())
        {
            await Assert.That(entity.Equals(nullEntity)).IsFalse();
            await Assert.That(entity.Equals((object?)null)).IsFalse();
            await Assert.That(entity == nullEntity).IsFalse();
            await Assert.That(nullEntity == null).IsTrue();
        }
    }

    [Test]
    public async Task Entity_SameInstance_IsEqual()
    {
        var entity = new TestingEntity();

        await Assert.That(entity.Equals(entity)).IsTrue();
    }

    [Test]
    public async Task Entity_ComparedToNonEntity_IsNotEqual()
    {
        var entity = new TestingEntity();

        await Assert.That(entity.Equals((object)entity.Id)).IsFalse();
    }

    private sealed class TestingEntity : Entity;

    private sealed class OtherTestingEntity : Entity;
}
