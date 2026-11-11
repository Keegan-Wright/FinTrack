using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Sharing;

namespace FinanceTracker.Tests.Unit.Client.Sharing;

public class SharingTests
{
    [Test]
    [Arguments(LinkState.Linked, "@sam · linked since 3 Nov 2026 · nothing shared yet")]
    [Arguments(LinkState.Invited, "@sam · invited 3 Nov 2026, waiting for them")]
    [Arguments(LinkState.InvitedYou, "@sam · invited you 3 Nov 2026")]
    public async Task StateText_SaysWhereItStandsAndSinceWhen(LinkState state, string expected)
    {
        using CultureScope culture = new();

        await Assert.That(FinanceTracker.Client.Pages.Sharing.StateText(Link(state))).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, 0, "nothing shared yet")]
    [Arguments(1, 0, "you share 1 account, they share 0")]
    [Arguments(3, 2, "you share 3 accounts, they share 2")]
    [Arguments(0, 1, "you share 0 accounts, they share 1")]
    public async Task SharedSummary_SaysWhatsSharedBothWays(int yours, int theirs, string expected) =>
        await Assert.That(FinanceTracker.Client.Pages.Sharing.SharedSummary(
            Link(LinkState.Linked) with { AccountsYouShare = yours, AccountsTheyShare = theirs })).IsEqualTo(expected);

    [Test]
    [Arguments(LinkState.Invited, "Withdraw")]
    [Arguments(LinkState.Linked, "End link")]
    public async Task EndText_WithdrawsAnInviteOrEndsALink(LinkState state, string expected) =>
        await Assert.That(FinanceTracker.Client.Pages.Sharing.EndText(Link(state))).IsEqualTo(expected);

    [Test]
    [Arguments(InviteRefusal.NoSuchUser, "No one here has the username or email sam")]
    [Arguments(InviteRefusal.Yourself, "That's you")]
    [Arguments(InviteRefusal.AlreadyLinked, "You're already linked with them")]
    [Arguments(InviteRefusal.AlreadyInvited, "There's already an invite waiting between you")]
    public async Task RefusalText_SaysWhyItDidntGo(InviteRefusal refusal, string expected) =>
        await Assert.That(FinanceTracker.Client.Pages.Sharing.RefusalText(refusal, "sam")).IsEqualTo(expected);

    private static UserLink Link(LinkState state) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Sam Taylor",
        Username = "sam",
        State = state,
        Since = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)
    };
}
