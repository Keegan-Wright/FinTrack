using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Components.Sharing;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Sharing;

/// <summary>
/// What's shared with one linked person, on their own page.
/// </summary>
public class SharingWithPageTests : ComponentTestBase
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
    private readonly ShareableItem _holiday = Item(SharedItemKind.Classification, "Holiday", ShareMode.ReadOnly);
    private readonly ShareableItem _kids = Item(SharedItemKind.Classification, "Kids", null);
    private readonly ShareableItem _shops = Item(SharedItemKind.Rule, "Shops", null);
    private readonly ShareableItem _rent = Item(SharedItemKind.Bill, "Rent", ShareMode.ReadOnly) with
    {
        FollowsAccount = "Joint"
    };

    private readonly JointAccountSuggestion _suggestion = new()
    {
        YourAccountId = Guid.NewGuid(),
        YourAccountName = "Joint account",
        TheirAccountId = Guid.NewGuid(),
        TheirAccountName = "Joint"
    };

    private readonly MergedJointAccount _merged = new()
    {
        JointAccountId = Guid.NewGuid(), Name = "Joint", ShownIsYours = false
    };

    public SharingWithPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_sharingService);
        _sharingService.GetLinksAsync(Arg.Any<CancellationToken>()).Returns(_ => new[] { _linked }.ToAsyncEnumerable());
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns(Shares());
        _sharingService.ChangeAccountSharesAsync(_linked.Id, Arg.Any<ChangeAccountShares>(),
            Arg.Any<CancellationToken>()).Returns(Shares());
        _sharingService.ChangeItemSharesAsync(_linked.Id, Arg.Any<ChangeItemShares>(), Arg.Any<CancellationToken>())
            .Returns(Shares());
        _sharingService.MergeJointAccountsAsync(_linked.Id, Arg.Any<MergeJointAccounts>(),
            Arg.Any<CancellationToken>()).Returns(Shares(merged: [_merged]));
        _sharingService.SplitJointAccountAsync(_linked.Id, _merged.JointAccountId, Arg.Any<CancellationToken>())
            .Returns(Shares(suggestions: [_suggestion]));
    }

    [Test]
    public async Task Page_ShowsWhatsSharedEachWay_WithCountsOnTheTabs()
    {
        // Act
        IRenderedComponent<SharingWith> cut = RenderPage();

        // Assert
        List<string> tabs = Tabs(cut);
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Sharing with Sam Taylor");
            await Assert.That(cut.Markup).Contains("@sam · linked since");
            await Assert.That(tabs).IsEquivalentTo(
                ["Accounts · 1/3", "Tags · 1/2", "Rules · 0/1", "Bills · 1/1", "From Sam"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(Checkbox(cut, "Share Savings").IsChecked()).IsTrue();
            await Assert.That(cut.FindAll(".ft-save-bar")).IsEmpty();
        }
    }

    [Test]
    public async Task Ticking_ShowsTheSaveBar_AndSavingSendsOnlyWhatChanged()
    {
        // Arrange
        IRenderedComponent<SharingWith> cut = RenderPage();

        // Act
        Checkbox(cut, "Share Current").Change(true);
        string bar = cut.Find(".ft-save-bar").TextContent;
        Button(cut, "Save").Click();
        cut.WaitForAssertion(() => _sharingService.ReceivedWithAnyArgs(1)
            .ChangeAccountSharesAsync(default, default!, default));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bar).Contains("1 change not saved");
            await _sharingService.Received(1).ChangeAccountSharesAsync(_linked.Id,
                Arg.Is<ChangeAccountShares>(x => x.Accounts.Count == 2 &&
                                                 x.Accounts.Any(a => a.AccountId == _current.AccountId)),
                Arg.Any<CancellationToken>());
            await _sharingService.DidNotReceiveWithAnyArgs().ChangeItemSharesAsync(default, default!, default);
        }
    }

    [Test]
    public async Task Discard_PutsTheTicksBack()
    {
        // Arrange
        IRenderedComponent<SharingWith> cut = RenderPage();
        Checkbox(cut, "Share Savings").Change(false);

        // Act
        Button(cut, "Discard").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Checkbox(cut, "Share Savings").IsChecked()).IsTrue();
            await Assert.That(cut.FindAll(".ft-save-bar")).IsEmpty();
        }
    }

    [Test]
    public async Task ChangesOnSeveralTabs_AreSavedTogether()
    {
        // Arrange
        IRenderedComponent<SharingWith> cut = RenderPage();
        OpenTab(cut, "Tags");
        Checkbox(cut, "Share Kids").Change(true);
        OpenTab(cut, "Rules");
        Checkbox(cut, "Share Shops").Change(true);

        // Act
        Button(cut, "Save").Click();
        cut.WaitForAssertion(() => _sharingService.ReceivedWithAnyArgs(1)
            .ChangeItemSharesAsync(default, default!, default));

        // Assert
        await _sharingService.Received(1).ChangeItemSharesAsync(_linked.Id, Arg.Is<ChangeItemShares>(x =>
                x.Items.Count == 3 &&
                x.Items.Any(i => i.ItemId == _kids.ItemId) &&
                x.Items.Any(i => i.ItemId == _shops.ItemId && i.Kind == SharedItemKind.Rule) &&
                x.Items.Any(i => i.ItemId == _holiday.ItemId) &&
                x.Items.All(i => i.ItemId != _rent.ItemId)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Bills_OneGoingWithAnAccount_SaysSoAndCantBeUnticked()
    {
        // Arrange
        IRenderedComponent<SharingWith> cut = RenderPage();

        // Act
        OpenTab(cut, "Bills");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Goes with Joint · can see");
            await Assert.That(cut.Find("input[aria-label='Rent goes with Joint']").HasAttribute("disabled")).IsTrue();
        }
    }

    [Test]
    public async Task ALongList_CanBeSearched()
    {
        // Arrange
        List<ShareableItem> tags = Enumerable.Range(1, ShareChoiceList.SearchFrom + 1)
            .Select(i => Item(SharedItemKind.Classification, $"Tag {i}", null))
            .Append(Item(SharedItemKind.Classification, "Holiday", null))
            .ToList();
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns(Shares() with
        {
            YourItems = tags
        });
        IRenderedComponent<SharingWith> cut = RenderPage();
        OpenTab(cut, "Tags");

        // Act
        cut.Find(".ft-search input").Input("holi");

        // Assert
        await Assert.That(cut.FindAll("input[aria-label^='Share ']").Select(static x => x.GetAttribute("aria-label") ?? string.Empty))
            .IsEquivalentTo(["Share Holiday"]);
    }

    [Test]
    public async Task JointAccounts_MergeStraightAway_ButNotWithChangesUnsaved()
    {
        // Arrange
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>())
            .Returns(Shares(suggestions: [_suggestion]));
        IRenderedComponent<SharingWith> cut = RenderPage();
        Checkbox(cut, "Share Current").Change(true);
        OpenTab(cut, "Joint accounts · 1 to merge");
        bool waitsWhileUnsaved = Button(cut, "Merge").HasAttribute("disabled");
        Button(cut, "Discard").Click();

        // Act
        Button(cut, "Merge").Click();
        cut.WaitForState(() => cut.Markup.Contains("Counted once"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(waitsWhileUnsaved).IsTrue();
            await _sharingService.Received(1).MergeJointAccountsAsync(_linked.Id,
                Arg.Is<MergeJointAccounts>(x => x.Accounts.Count == 1 &&
                                                x.Accounts[0].YourAccountId == _suggestion.YourAccountId),
                Arg.Any<CancellationToken>());
            await Assert.That(cut.Markup).Contains("Counted once, from Sam Taylor's connection");
        }
    }

    [Test]
    public async Task JointAccounts_Split()
    {
        // Arrange
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns(Shares(merged: [_merged]));
        IRenderedComponent<SharingWith> cut = RenderPage();
        OpenTab(cut, "Joint accounts");

        // Act
        Button(cut, "Split").Click();
        cut.WaitForState(() => cut.Markup.Contains("Could be merged"));

        // Assert
        await _sharingService.Received(1).SplitJointAccountAsync(_linked.Id, _merged.JointAccountId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FromThem_ListsWhatTheyShare()
    {
        // Arrange
        IRenderedComponent<SharingWith> cut = RenderPage();

        // Act
        OpenTab(cut, "From Sam");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Bank · can see");
            await Assert.That(cut.Markup).Contains("Groceries");
            await Assert.That(cut.Markup).Contains("Can see and change");
        }
    }

    [Test]
    public async Task EndLink_OnceConfirmed_EndsItAndGoesBackToEveryone()
    {
        // Arrange
        _sharingService.EndAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<SharingWith> cut = RenderPage();

        // Act
        Task ending = Button(cut, "End link").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "End link");
        await ending;

        // Assert
        using (Assert.Multiple())
        {
            await _sharingService.Received(1).EndAsync(_linked.Id, Arg.Any<CancellationToken>());
            await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/Sharing");
        }
    }

    [Test]
    public async Task NotLinkedAnyMore_SaysSo()
    {
        // Arrange
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns((LinkShares?)null);

        // Act
        IRenderedComponent<SharingWith> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("You're not linked with them any more");
    }

    private LinkShares Shares(List<JointAccountSuggestion>? suggestions = null,
        List<MergedJointAccount>? merged = null) => new()
    {
        LinkId = _linked.Id,
        Name = _linked.Name,
        YourAccounts = [_current, _savings, _birthday],
        TheirAccounts = [new AccountSharedWithYou { Name = "Joint", BankName = "Bank", Mode = ShareMode.ReadOnly }],
        YourItems = [_holiday, _kids, _shops, _rent],
        TheirItems =
        [
            new ItemSharedWithYou
            {
                Kind = SharedItemKind.Classification, Name = "Groceries", Mode = ShareMode.AllowChanges
            }
        ],
        JointSuggestions = suggestions ?? [],
        JointAccounts = merged ?? []
    };

    private static ShareableAccount Account(string name, ShareMode? mode) => new()
    {
        AccountId = Guid.NewGuid(),
        Name = name,
        BankName = "Bank",
        Mode = mode
    };

    private static ShareableItem Item(SharedItemKind kind, string name, ShareMode? mode) => new()
    {
        Kind = kind,
        ItemId = Guid.NewGuid(),
        Name = name,
        Mode = mode
    };

    private IRenderedComponent<SharingWith> RenderPage() =>
        Render<SharingWith>(p => p
            .Add(x => x.LinkId, _linked.Id)
            .AddCascadingValue(_applicationState));

    private static List<string> Tabs(IRenderedComponent<SharingWith> cut) =>
        cut.FindAll(".mud-tab").Select(static x => x.TextContent.Trim()).ToList();

    private static void OpenTab(IRenderedComponent<SharingWith> cut, string name) =>
        cut.FindAll(".mud-tab").First(x => x.TextContent.Trim().StartsWith(name, StringComparison.Ordinal)).Click();

    private static IElement Button<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button").First(x => x.TextContent.Trim() == text);

    // MudBlazor puts the label beside the input, a thing's own checkbox is named for it
    private static IElement Checkbox<T>(IRenderedComponent<T> cut, string name) where T : IComponent =>
        cut.FindAll("input[type=checkbox]").First(x => x.GetAttribute("aria-label") == name);
}
