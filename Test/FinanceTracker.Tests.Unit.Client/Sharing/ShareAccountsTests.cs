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
/// Choosing which accounts to share with someone, from the Sharing page.
/// </summary>
public class ShareAccountsTests : ComponentTestBase
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

    public ShareAccountsTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_sharingService);
        _sharingService.GetLinksAsync(Arg.Any<CancellationToken>()).Returns(_ => new[] { _linked }.ToAsyncEnumerable());
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns(new LinkShares
        {
            LinkId = _linked.Id,
            Name = _linked.Name,
            YourAccounts = [_current, _savings, _birthday],
            TheirAccounts = [new AccountSharedWithYou { Name = "Joint", BankName = "Bank", Mode = ShareMode.ReadOnly }]
        });
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
    [Arguments(ShareMode.ReadOnly, "Can see")]
    [Arguments(ShareMode.AllowChanges, "Can see and change")]
    public async Task ModeText_SaysWhatTheyCanDo(ShareMode mode, string expected) =>
        await Assert.That(ShareAccountsDialog.ModeText(mode)).IsEqualTo(expected);

    private static ShareableAccount Account(string name, ShareMode? mode) => new()
    {
        AccountId = Guid.NewGuid(),
        Name = name,
        BankName = "Bank",
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
