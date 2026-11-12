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
using SharingPage = FinanceTracker.Client.Pages.Sharing;

namespace FinanceTracker.Tests.Unit.Client.Sharing;

/// <summary>
/// Merging and splitting a joint account both people connected, from the Sharing page.
/// </summary>
public class JointAccountsTests : ComponentTestBase
{
    private readonly ISharingService _sharingService = Substitute.For<ISharingService>();
    private readonly ApplicationState _applicationState = new();

    private readonly UserLink _linked = new()
    {
        Id = Guid.NewGuid(),
        Name = "Sam Taylor",
        Username = "sam",
        State = LinkState.Linked,
        Since = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        AccountsTheyShare = 1,
        JointSuggestions = 1
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

    public JointAccountsTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_sharingService);
        _sharingService.GetLinksAsync(Arg.Any<CancellationToken>()).Returns(_ => new[] { _linked }.ToAsyncEnumerable());
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>())
            .Returns(Shares(suggestions: [_suggestion]));
        _sharingService.MergeJointAccountsAsync(_linked.Id, Arg.Any<MergeJointAccounts>(), Arg.Any<CancellationToken>())
            .Returns(Shares(merged: [_merged]));
        _sharingService.SplitJointAccountAsync(_linked.Id, _merged.JointAccountId, Arg.Any<CancellationToken>())
            .Returns(Shares(suggestions: [_suggestion]));
    }

    [Test]
    public async Task Page_SaysThereIsOneToMerge_AndOffersJointAccounts()
    {
        // Act
        IRenderedComponent<SharingPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("a joint account to merge");
            await Assert.That(cut.FindAll("button").Select(static x => x.TextContent.Trim()))
                .Contains("Joint accounts");
        }
    }

    [Test]
    public async Task Merge_MergesThatPairAndShowsItMerged()
    {
        // Arrange
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<SharingPage> cut = RenderPage();
        Button(cut, "Joint accounts").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Could be merged"));

        // Act
        Button(dialogs, "Merge").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Counted once"));

        // Assert
        using (Assert.Multiple())
        {
            await _sharingService.Received(1).MergeJointAccountsAsync(_linked.Id,
                Arg.Is<MergeJointAccounts>(x => x.Accounts.Count == 1 &&
                                                x.Accounts[0].YourAccountId == _suggestion.YourAccountId &&
                                                x.Accounts[0].TheirAccountId == _suggestion.TheirAccountId),
                Arg.Any<CancellationToken>());
            await Assert.That(dialogs.Markup).Contains("Counted once, from Sam Taylor's connection");
            await Assert.That(dialogs.Markup).DoesNotContain("Could be merged");
        }
    }

    [Test]
    public async Task MergeAll_MergesEverySuggestionInOneGo()
    {
        // Arrange
        JointAccountSuggestion christmas = _suggestion with
        {
            YourAccountId = Guid.NewGuid(), YourAccountName = "Christmas", TheirAccountId = Guid.NewGuid(),
            TheirAccountName = "Christmas"
        };
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>())
            .Returns(Shares(suggestions: [_suggestion, christmas]));
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<SharingPage> cut = RenderPage();
        Button(cut, "Joint accounts").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Merge all"));

        // Act
        Button(dialogs, "Merge all").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Counted once"));

        // Assert
        await _sharingService.Received(1).MergeJointAccountsAsync(_linked.Id,
            Arg.Is<MergeJointAccounts>(x => x.Accounts.Count == 2), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Split_SplitsItAgain()
    {
        // Arrange
        _sharingService.GetSharesAsync(_linked.Id, Arg.Any<CancellationToken>()).Returns(Shares(merged: [_merged]));
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<SharingPage> cut = RenderPage();
        Button(cut, "Joint accounts").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Merged"));

        // Act
        Button(dialogs, "Split").Click();
        dialogs.WaitForState(() => dialogs.Markup.Contains("Could be merged"));

        // Assert
        await _sharingService.Received(1).SplitJointAccountAsync(_linked.Id, _merged.JointAccountId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(0, 0, "")]
    [Arguments(1, 0, " · a joint account to merge")]
    [Arguments(2, 1, " · 2 joint accounts to merge")]
    [Arguments(0, 1, " · 1 joint account")]
    [Arguments(0, 3, " · 3 joint accounts")]
    public async Task JointSummary_SaysWhatsToMergeFirst(int suggestions, int merged, string expected) =>
        await Assert.That(SharingPage.JointSummary(_linked with
        {
            JointSuggestions = suggestions, JointAccounts = merged
        })).IsEqualTo(expected);

    [Test]
    public async Task MergedText_SaysWhoseConnectionItCountsFrom() =>
        await Assert.That(JointAccountsDialog.MergedText(_merged with { ShownIsYours = true }, "Sam Taylor"))
            .IsEqualTo("Counted once, from your connection");

    private LinkShares Shares(List<JointAccountSuggestion>? suggestions = null,
        List<MergedJointAccount>? merged = null) => new()
    {
        LinkId = _linked.Id,
        Name = _linked.Name,
        YourAccounts = [],
        TheirAccounts = [],
        JointSuggestions = suggestions ?? [],
        JointAccounts = merged ?? []
    };

    private IRenderedComponent<SharingPage> RenderPage() =>
        Render<SharingPage>(p => p.AddCascadingValue(_applicationState));

    private static IElement Button<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button").First(x => x.TextContent.Trim() == text);
}
