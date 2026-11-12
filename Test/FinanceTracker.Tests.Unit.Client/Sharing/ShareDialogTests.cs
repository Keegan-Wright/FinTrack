using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Components.Dialogs;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Sharing;

/// <summary>
/// Choosing which accounts and tags to share with someone, from the Sharing page.
/// </summary>
public class ShareDialogTests : ComponentTestBase
{
    private readonly ISharingService _sharingService = Substitute.For<ISharingService>();
    private readonly ApplicationState _applicationState = new();

    private readonly UserLink _linked = new()
    {
        Id = Guid.NewGuid(),
        Name = "Sam Taylor",
        Username = "sam",
        State = LinkState.Linked,
        Since = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)
    };

    private readonly ShareableAccount _current = Account("Current", null);
    private readonly ShareableAccount _savings = Account("Savings", ShareMode.AllowChanges);
    private readonly ShareableAccount _birthday = Account("Birthday fund", null);
    private readonly ShareableItem _holiday = TagItem("Holiday", ShareMode.ReadOnly);
    private readonly ShareableItem _kids = TagItem("Kids", null);
    private readonly ShareableItem _shops = new()
    {
        Kind = SharedItemKind.Rule, ItemId = Guid.NewGuid(), Name = "Shops", Mode = null
    };

    public ShareDialogTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_sharingService);
        _sharingService.GetLinksAsync(Arg.Any<CancellationToken>()).Returns(_ => new[] { _linked }.ToAsyncEnumerable());
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns(new LinkShares
        {
            LinkId = _linked.Id,
            Name = _linked.Name,
            YourAccounts = [_current, _savings, _birthday],
            TheirAccounts = [new AccountSharedWithYou { Name = "Joint", BankName = "Bank", Mode = ShareMode.ReadOnly }],
            YourItems = [_holiday, _kids, _shops],
            TheirItems =
            [
                new ItemSharedWithYou
                {
                    Kind = SharedItemKind.Classification, Name = "Groceries", Mode = ShareMode.AllowChanges
                }
            ]
        });
        _sharingService.ChangeItemSharesAsync(_linked.Id, Arg.Any<ChangeItemShares>(), Arg.Any<CancellationToken>())
            .Returns(new LinkShares { LinkId = _linked.Id, Name = _linked.Name, YourAccounts = [], TheirAccounts = [] });
        _sharingService.ChangeAccountSharesAsync(_linked.Id, Arg.Any<ChangeAccountShares>(), Arg.Any<CancellationToken>())
            .Returns(call => new LinkShares
            {
                LinkId = _linked.Id,
                Name = _linked.Name,
                YourAccounts = [],
                TheirAccounts = []
            });
    }

    [Test]
    public async Task Dialog_StartsWithWhatsShared_OnlySharedAccountsGetAToggle()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = RenderPage();

        // Act
        Button(cut, "Share").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Birthday fund"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Checkbox(dialogs, "Share Savings").IsChecked()).IsTrue();
            await Assert.That(Checkbox(dialogs, "Share Current").IsChecked()).IsFalse();
            await Assert.That(dialogs.FindAll(".mud-toggle-group")).Count().IsEqualTo(1);
            await Assert.That(dialogs.Markup).Contains("1 of 3 shared");
            await Assert.That(dialogs.Markup).Contains("Joint");
            await Assert.That(dialogs.Markup).Contains("Bank · can see");
            await Assert.That(dialogs.Markup).Contains("Tag · can see and change");
        }
    }

    [Test]
    public async Task Tags_TickingOne_SavesOnlyTheTags()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = RenderPage();
        Button(cut, "Share").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Birthday fund"));
        dialogs.FindAll(".mud-tab").First(x => x.TextContent.Trim() == "Tags").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Kids"));

        // Act
        Checkbox(dialogs, "Share Kids").Change(true);
        Button(dialogs, "OK").Click();
        cut.WaitForAssertion(() => _sharingService.ReceivedWithAnyArgs(1)
            .ChangeItemSharesAsync(default, default!, default));

        // Assert
        await _sharingService.Received(1).ChangeItemSharesAsync(_linked.Id, Arg.Is<ChangeItemShares>(x =>
                x.Items.Count == 2 &&
                x.Items.All(i => i.Kind == SharedItemKind.Classification && i.Mode == ShareMode.ReadOnly) &&
                x.Items.Any(i => i.ItemId == _kids.ItemId) && x.Items.Any(i => i.ItemId == _holiday.ItemId)),
            Arg.Any<CancellationToken>());
        await _sharingService.DidNotReceiveWithAnyArgs().ChangeAccountSharesAsync(default, default!, default);
    }

    [Test]
    public async Task Rules_TickingOne_SavesItWithTheTagsAsTheyWere()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = RenderPage();
        Button(cut, "Share").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Birthday fund"));
        dialogs.FindAll(".mud-tab").First(x => x.TextContent.Trim() == "Rules").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Shops"));

        // Act
        Checkbox(dialogs, "Share Shops").Change(true);
        Button(dialogs, "OK").Click();
        cut.WaitForAssertion(() => _sharingService.ReceivedWithAnyArgs(1)
            .ChangeItemSharesAsync(default, default!, default));

        // Assert
        await _sharingService.Received(1).ChangeItemSharesAsync(_linked.Id, Arg.Is<ChangeItemShares>(x =>
                x.Items.Count == 2 &&
                x.Items.Any(i => i.Kind == SharedItemKind.Rule && i.ItemId == _shops.ItemId) &&
                x.Items.Any(i => i.Kind == SharedItemKind.Classification && i.ItemId == _holiday.ItemId)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NothingChanged_SavesNothing()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = RenderPage();
        Button(cut, "Share").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Birthday fund"));

        // Act
        Button(dialogs, "OK").Click();
        dialogs.WaitForState(() => !dialogs.Markup.Contains("Birthday fund"));

        // Assert
        using (Assert.Multiple())
        {
            await _sharingService.DidNotReceiveWithAnyArgs().ChangeAccountSharesAsync(default, default!, default);
            await _sharingService.DidNotReceiveWithAnyArgs().ChangeItemSharesAsync(default, default!, default);
        }
    }

    [Test]
    public async Task SelectAll_SharesEveryAccountListed_KeepingModesAlreadyChosen()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = RenderPage();
        Button(cut, "Share").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Select all"));

        // Act
        Checkbox(dialogs, "Select all").Change(true);
        Button(dialogs, "OK").Click();
        cut.WaitForAssertion(() => _sharingService.ReceivedWithAnyArgs(1)
            .ChangeAccountSharesAsync(default, default!, default));

        // Assert
        await _sharingService.Received(1).ChangeAccountSharesAsync(_linked.Id, Arg.Is<ChangeAccountShares>(x =>
                x.Accounts.Count == 3 &&
                x.Accounts.Single(a => a.AccountId == _savings.AccountId).Mode == ShareMode.AllowChanges &&
                x.Accounts.Single(a => a.AccountId == _current.AccountId).Mode == ShareMode.ReadOnly),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Unticking_StopsSharingIt()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<FinanceTracker.Client.Pages.Sharing> cut = RenderPage();
        Button(cut, "Share").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Savings"));

        // Act
        Checkbox(dialogs, "Share Savings").Change(false);
        Button(dialogs, "OK").Click();
        cut.WaitForAssertion(() => _sharingService.ReceivedWithAnyArgs(1)
            .ChangeAccountSharesAsync(default, default!, default));

        // Assert
        await _sharingService.Received(1).ChangeAccountSharesAsync(_linked.Id,
            Arg.Is<ChangeAccountShares>(x => x.Accounts.Count == 0), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(0, "Nothing shared with Sam Taylor")]
    [Arguments(1, "Sharing 1 account with Sam Taylor")]
    [Arguments(3, "Sharing 3 accounts with Sam Taylor")]
    public async Task SharedText_SaysHowManyAccountsAreShared(int shared, string expected)
    {
        LinkShares shares = new()
        {
            LinkId = Guid.NewGuid(),
            Name = "Sam Taylor",
            YourAccounts = Enumerable.Range(0, 4)
                .Select(i => Account($"Account {i}", i < shared ? ShareMode.ReadOnly : null)).ToList(),
            TheirAccounts = []
        };

        await Assert.That(FinanceTracker.Client.Pages.Sharing.SharedText(shares)).IsEqualTo(expected);
    }

    [Test]
    public async Task SharedText_CountsTagsToo()
    {
        LinkShares shares = new()
        {
            LinkId = Guid.NewGuid(),
            Name = "Sam Taylor",
            YourAccounts = [Account("Current", ShareMode.ReadOnly)],
            TheirAccounts = [],
            YourItems = [TagItem("Holiday", ShareMode.ReadOnly), TagItem("Kids", ShareMode.AllowChanges)]
        };

        await Assert.That(FinanceTracker.Client.Pages.Sharing.SharedText(shares))
            .IsEqualTo("Sharing 1 account and 2 tags with Sam Taylor");
    }

    [Test]
    public async Task SharedText_ListsEachKind()
    {
        LinkShares shares = new()
        {
            LinkId = Guid.NewGuid(),
            Name = "Sam Taylor",
            YourAccounts = [Account("Current", ShareMode.ReadOnly)],
            TheirAccounts = [],
            YourItems =
            [
                TagItem("Holiday", ShareMode.ReadOnly),
                new ShareableItem { Kind = SharedItemKind.Rule, ItemId = Guid.NewGuid(), Name = "Shops",
                    Mode = ShareMode.ReadOnly }
            ]
        };

        await Assert.That(FinanceTracker.Client.Pages.Sharing.SharedText(shares))
            .IsEqualTo("Sharing 1 account, 1 tag and 1 rule with Sam Taylor");
    }

    [Test]
    [Arguments(ShareMode.ReadOnly, "Can see")]
    [Arguments(ShareMode.AllowChanges, "Can see and change")]
    public async Task ModeText_SaysWhatTheyCanDo(ShareMode mode, string expected) =>
        await Assert.That(ShareDialog.ModeText(mode)).IsEqualTo(expected);

    private static ShareableAccount Account(string name, ShareMode? mode) => new()
    {
        AccountId = Guid.NewGuid(),
        Name = name,
        BankName = "Bank",
        Mode = mode
    };

    private static ShareableItem TagItem(string name, ShareMode? mode) => new()
    {
        Kind = SharedItemKind.Classification,
        ItemId = Guid.NewGuid(),
        Name = name,
        Mode = mode
    };

    private IRenderedComponent<FinanceTracker.Client.Pages.Sharing> RenderPage() =>
        Render<FinanceTracker.Client.Pages.Sharing>(p => p.AddCascadingValue(_applicationState));

    private static IElement Button<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button").First(x => x.TextContent.Trim() == text);

    // MudBlazor puts the label beside the input, an account's own checkbox is named for it
    private static IElement Checkbox(IRenderedComponent<MudDialogProvider> dialogs, string name) =>
        dialogs.FindAll("input[type=checkbox]")
            .First(x => x.GetAttribute("aria-label") == name ||
                        x.ParentElement?.ParentElement?.TextContent.Trim() == name ||
                        x.Closest("label")?.TextContent.Trim() == name);
}
