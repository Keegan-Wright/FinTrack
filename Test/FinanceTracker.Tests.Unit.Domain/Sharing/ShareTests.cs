using FinanceTracker.Domain.Sharing;

namespace FinanceTracker.Tests.Unit.Domain.Sharing;

[Category("Domain")]
public class ShareTests
{
    [Test]
    public async Task New_IsOwnedByWhoSharedItThroughTheLink()
    {
        Guid sharedBy = Guid.NewGuid();
        Guid linkId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        Share share = new(sharedBy, linkId, ShareItemType.BankAccount, accountId, ShareMode.ReadOnly, DateTime.UtcNow);

        using (Assert.Multiple())
        {
            await Assert.That(share.UserId).IsEqualTo(sharedBy);
            await Assert.That(share.LinkId).IsEqualTo(linkId);
            await Assert.That(share.ItemId).IsEqualTo(accountId);
            await Assert.That(share.Mode).IsEqualTo(ShareMode.ReadOnly);
        }
    }

    [Test]
    public async Task ChangeMode_ChangesWhatTheyCanDo()
    {
        Share share = new(Guid.NewGuid(), Guid.NewGuid(), ShareItemType.BankAccount, Guid.NewGuid(),
            ShareMode.ReadOnly, DateTime.UtcNow);

        share.ChangeMode(ShareMode.AllowChanges);

        await Assert.That(share.Mode).IsEqualTo(ShareMode.AllowChanges);
    }

    [Test]
    public void New_WithoutALinkOrItem_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Share(Guid.NewGuid(), Guid.Empty,
            ShareItemType.BankAccount, Guid.NewGuid(), ShareMode.ReadOnly, DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Share(Guid.NewGuid(), Guid.NewGuid(),
            ShareItemType.BankAccount, Guid.Empty, ShareMode.ReadOnly, DateTime.UtcNow));
    }
}
