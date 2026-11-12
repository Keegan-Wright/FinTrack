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

    [Test]
    public async Task OpenGap_FillsAfterTheShownCopysLastTransaction_AndTakesTheBalanceFromThatDay()
    {
        JointAccount joint = New();
        DateTime lastShown = Now.AddDays(-5);

        joint.OpenGap(lastShown, Now);

        using (Assert.Multiple())
        {
            await Assert.That(joint.HasOpenGap).IsTrue();
            await Assert.That(joint.InGap(lastShown)).IsFalse();
            await Assert.That(joint.InGap(lastShown.AddMinutes(1))).IsTrue();
            await Assert.That(joint.InGap(Now.AddDays(30))).IsTrue();
            await Assert.That(joint.BalanceFromHiddenOn(DateOnly.FromDateTime(Now).AddDays(-1))).IsFalse();
            await Assert.That(joint.BalanceFromHiddenOn(DateOnly.FromDateTime(Now))).IsTrue();
        }
    }

    [Test]
    public async Task CloseGap_StopsFillingWhereTheShownCopysOwnStartAgain()
    {
        // Arrange
        JointAccount joint = New();
        DateTime lastShown = Now.AddDays(-60);
        DateTime firstAgain = Now.AddDays(-20);
        DateTime synced = Now.AddDays(1);
        joint.OpenGap(lastShown, Now.AddDays(-50));

        // Act
        joint.CloseGap(firstAgain, synced);

        // Assert, the stretch the bank wouldn't give back stays filled
        using (Assert.Multiple())
        {
            await Assert.That(joint.HasOpenGap).IsFalse();
            await Assert.That(joint.InGap(Now.AddDays(-30))).IsTrue();
            await Assert.That(joint.InGap(firstAgain)).IsFalse();
            await Assert.That(joint.InGap(Now)).IsFalse();
            await Assert.That(joint.BalanceFromHiddenOn(DateOnly.FromDateTime(Now))).IsTrue();
            await Assert.That(joint.BalanceFromHiddenOn(DateOnly.FromDateTime(synced))).IsFalse();
        }
    }

    [Test]
    public async Task OpenGap_AgainAfterOneClosed_KeepsBoth()
    {
        // Arrange
        JointAccount joint = New();
        joint.OpenGap(Now.AddDays(-100), Now.AddDays(-90));
        joint.CloseGap(Now.AddDays(-80), Now.AddDays(-70));

        // Act
        joint.OpenGap(Now.AddDays(-10), Now);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(joint.Gaps.Count).IsEqualTo(2);
            await Assert.That(joint.InGap(Now.AddDays(-95))).IsTrue();
            await Assert.That(joint.InGap(Now.AddDays(-50))).IsFalse();
            await Assert.That(joint.InGap(Now.AddDays(-5))).IsTrue();
        }
    }

    [Test]
    public void OpenGap_WhileOneIsOpen_Throws()
    {
        JointAccount joint = New();
        joint.OpenGap(Now.AddDays(-5), Now);

        Assert.Throws<InvalidOperationException>(() => joint.OpenGap(Now.AddDays(-1), Now));
    }

    [Test]
    public void CloseGap_WithNoneOpen_Throws()
    {
        JointAccount joint = New();

        Assert.Throws<InvalidOperationException>(() => joint.CloseGap(Now, Now));
    }

    [Test]
    public void CloseGap_BeforeItStarted_Throws()
    {
        JointAccount joint = New();
        joint.OpenGap(Now.AddDays(-5), Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => joint.CloseGap(Now.AddDays(-6), Now));
    }

    private static JointAccount New() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
}
