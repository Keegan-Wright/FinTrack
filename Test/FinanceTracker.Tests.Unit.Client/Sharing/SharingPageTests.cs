using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Sharing;

public class SharingPageTests : ComponentTestBase
{
    private readonly ISharingService _sharingService = Substitute.For<ISharingService>();
    private readonly ApplicationState _applicationState = new();

    private readonly UserLink _invitedYou = new()
    {
        Id = Guid.NewGuid(),
        Name = "Sam Taylor",
        Username = "sam",
        State = LinkState.InvitedYou,
        Since = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)
    };

    public SharingPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_sharingService);
    }

    [Test]
    public async Task Accept_AnswersTheInviteAndShowsThemLinked()
    {
        // Arrange
        using CultureScope culture = new();
        UserLink linked = _invitedYou with { State = LinkState.Linked };
        _sharingService.GetLinksAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new[] { _invitedYou }.ToAsyncEnumerable(), _ => new[] { linked }.ToAsyncEnumerable());
        _sharingService.AcceptAsync(_invitedYou.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = Render();

        // Act
        Button(cut, "Accept").Click();
        cut.WaitForState(() => cut.Markup.Contains("linked since"));

        // Assert
        using (Assert.Multiple())
        {
            await _sharingService.Received(1).AcceptAsync(_invitedYou.Id, Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).DoesNotContain("Waiting for you");
        }
    }

    [Test]
    public async Task Invite_Refused_SaysWhyUnderTheField()
    {
        // Arrange
        _sharingService.GetLinksAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<UserLink>());
        _sharingService.InviteAsync(Arg.Any<InviteToShare>(), Arg.Any<CancellationToken>())
            .Returns(InviteOutcome.Refused(InviteRefusal.NoSuchUser));
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = Render();

        // Act
        cut.Find("input").Input("nobody");
        Button(cut, "Invite").Click();
        cut.WaitForState(() => cut.Markup.Contains("No one here has"));

        // Assert
        await _sharingService.Received(1).InviteAsync(Arg.Is<InviteToShare>(x => x.Who == "nobody"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Invite_Sent_ListsThemAsWaiting()
    {
        // Arrange
        using CultureScope culture = new();
        _sharingService.GetLinksAsync(Arg.Any<CancellationToken>()).Returns(_ => AsyncEnumerable.Empty<UserLink>());
        UserLink sent = _invitedYou with { State = LinkState.Invited };
        _sharingService.InviteAsync(Arg.Any<InviteToShare>(), Arg.Any<CancellationToken>())
            .Returns(InviteOutcome.Sent(sent));
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = Render();

        // Act
        cut.Find("input").Input("  sam ");
        Button(cut, "Invite").Click();
        cut.WaitForState(() => cut.Markup.Contains("waiting for them"));

        // Assert
        using (Assert.Multiple())
        {
            await _sharingService.Received(1).InviteAsync(Arg.Is<InviteToShare>(x => x.Who == "sam"),
                Arg.Any<CancellationToken>());
            await Assert.That(Button(cut, "Withdraw")).IsNotNull();
        }
    }

    private IRenderedComponent<FinanceTracker.Client.Pages.Sharing> Render() =>
        Render<FinanceTracker.Client.Pages.Sharing>(p => p.AddCascadingValue(_applicationState));

    private static IElement Button(IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut, string text) =>
        cut.FindAll("button").First(x => x.TextContent.Trim() == text);
}
