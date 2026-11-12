using FinanceTracker.Domain.Sharing;

namespace FinanceTracker.Tests.Unit.Domain.Sharing;

[Category("Domain")]
public class JointAccountTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task New_KeepsWhichCopyIsShownAndWhichHidden()
    {
        Guid shown = Guid.NewGuid();
        Guid hidden = Guid.NewGuid();

        JointAccount joint = new(Guid.NewGuid(), Guid.NewGuid(), shown, hidden, Now);

        using (Assert.Multiple())
        {
            await Assert.That(joint.ShownAccountId).IsEqualTo(shown);
            await Assert.That(joint.HiddenAccountId).IsEqualTo(hidden);
            await Assert.That(joint.MergedAt).IsEqualTo(Now);
        }
    }

    [Test]
    public void New_TheSameAccountTwice_Throws()
    {
        Guid account = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            _ = new JointAccount(Guid.NewGuid(), Guid.NewGuid(), account, account, Now));
    }
}
