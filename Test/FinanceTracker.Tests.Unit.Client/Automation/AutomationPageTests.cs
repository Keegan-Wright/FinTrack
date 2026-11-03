using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using TUnit.Assertions.Enums;
using AutomationPage = FinanceTracker.Client.Pages.Automation;

namespace FinanceTracker.Tests.Unit.Client.Automation;

public class AutomationPageTests : ComponentTestBase
{
    private const string SyncName = "Sync all open banking details";
    private static readonly DateTime Due = new(2026, 11, 3, 14, 0, 0);

    private readonly IAutomationService _automationService = Substitute.For<IAutomationService>();
    private readonly ISyncNotifications _syncNotifications = Substitute.For<ISyncNotifications>();
    private readonly ManualTimeProvider _time = new();
    private readonly ApplicationState _applicationState = new();
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private Func<string, Task>? _onJobChanged;

    public AutomationPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_automationService);
        Services.AddSingleton(_syncNotifications);
        Services.AddSingleton<TimeProvider>(_time);

        _syncNotifications.SubscribeToJobsAsync(Arg.Any<Func<string, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _onJobChanged = call.Arg<Func<string, Task>>();
                return Substitute.For<IDisposable>();
            });

        _automationService.GetRunsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new AutomationRunPage([], 0));
    }

    [Test]
    public async Task Render_ShowsEachJobWithHowItsDoing()
    {
        // Arrange
        using CultureScope culture = new();
        AutomationRun lastRun = AutomationTextTests.Run(AutomationRunStatus.Done, Due, startedAt: Due,
            duration: TimeSpan.FromSeconds(4.5));
        Jobs(
            AutomationTextTests.Job(nextRunAt: Due.AddMinutes(15), lastRun: lastRun, successRate: 95, recentRuns: 20),
            AutomationTextTests.Job(AutomationTextTests.UserSyncFunction, scheduled: false));

        // Act
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains(SyncName);
            await Assert.That(cut.Markup).Contains("Sync user open banking details");
            await Assert.That(cut.Markup).Contains("0 */15 * * * *");
            await Assert.That(cut.Markup).Contains("3 Nov 14:15");
            await Assert.That(cut.Markup).Contains("3 Nov 14:00, took 4.5s");
            await Assert.That(cut.Markup).Contains("95% of the last 20 runs");
            await Assert.That(cut.Markup).Contains("2, after 30s, 60s");
            await Assert.That(cut.Markup).Contains("Enabled");
            await Assert.That(cut.Markup).Contains("By hand");
            await Assert.That(cut.FindAll("[aria-label^='Actions for']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task ScheduledJob_OffersEditInItsMenu()
    {
        // Arrange
        Jobs(AutomationTextTests.Job());
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Act
        IReadOnlyList<(string Text, bool Disabled)> items = await RowMenu.ItemsAsync(cut, _popovers, SyncName);

        // Assert
        await Assert.That(items).IsEquivalentTo([("Run now", false), ("Edit", false)], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(AutomationRunStatus.Running)]
    [Arguments(AutomationRunStatus.Retrying)]
    public async Task RunNow_WhileTheJobRuns_IsGreyedOut(AutomationRunStatus status)
    {
        // Arrange
        Jobs(AutomationTextTests.Job(lastRun: AutomationTextTests.Run(status, Due)));
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Act
        IReadOnlyList<(string Text, bool Disabled)> items = await RowMenu.ItemsAsync(cut, _popovers, SyncName);

        // Assert
        await Assert.That(items.Single(static x => x.Text == "Run now").Disabled).IsTrue();
    }

    [Test]
    public async Task RunNow_OnceConfirmed_QueuesTheJobAndOpensItsRuns()
    {
        // Arrange
        AutomationJobSummary job = AutomationTextTests.Job();
        Jobs(job);
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Act
        Task running = await RowMenu.StartAsync(cut, _popovers, SyncName, "Run now");
        await RowMenu.ConfirmAsync(_dialogs, "Run now");
        await running;

        // Assert
        using (Assert.Multiple())
        {
            await _automationService.Received(1).RunNowAsync(job.Schedule!.Id, Arg.Any<CancellationToken>());
            await Assert.That(cut.FindAll("button").Any(static x => x.TextContent.Trim() == "Hide runs")).IsTrue();
            await _automationService.Received().GetRunsAsync(AutomationTextTests.SyncFunction, 0, 25,
                Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task RunNow_Cancelled_QueuesNothing()
    {
        // Arrange
        Jobs(AutomationTextTests.Job());
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Act
        Task running = await RowMenu.StartAsync(cut, _popovers, SyncName, "Run now");
        await RowMenu.ConfirmAsync(_dialogs, "Cancel");
        await running;

        // Assert
        await _automationService.DidNotReceiveWithAnyArgs().RunNowAsync(default, default);
    }

    [Test]
    public async Task JobChanged_LoadsTheJobsAgain()
    {
        // Arrange
        using CultureScope culture = new();
        AutomationJobSummary before = AutomationTextTests.Job();
        AutomationJobSummary after = before with
        {
            LastRun = AutomationTextTests.Run(AutomationRunStatus.Running, Due, startedAt: Due)
        };
        _automationService.GetJobSummariesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { before }.ToAsyncEnumerable(), new[] { after }.ToAsyncEnumerable());
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => _onJobChanged!(AutomationTextTests.SyncFunction));

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Running"));
        await Assert.That(cut.Markup).DoesNotContain("Not run yet");
    }

    [Test]
    public async Task WhileAJobRuns_ChecksOnItUntilItsDone()
    {
        // Arrange
        AutomationJobSummary running = AutomationTextTests.Job(
            lastRun: AutomationTextTests.Run(AutomationRunStatus.Running, Due));
        AutomationJobSummary done = running with { LastRun = AutomationTextTests.Run(AutomationRunStatus.Done, Due) };
        _automationService.GetJobSummariesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { running }.ToAsyncEnumerable(), new[] { done }.ToAsyncEnumerable());
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Act, a check while it runs and one after it's done
        _time.Fire();
        cut.WaitForState(() => cut.Markup.Contains("Done"));
        _time.Fire();

        // Assert, loaded on render and on the first check only
        await Task.Delay(50);
        _automationService.Received(2).GetJobSummariesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ShowRuns_ListsTheJobsRunsAndOpensAnError()
    {
        // Arrange
        using CultureScope culture = new();
        Jobs(AutomationTextTests.Job());
        AutomationRun failed = AutomationTextTests.Run(AutomationRunStatus.Failed, Due, isManual: true,
            startedAt: Due, duration: TimeSpan.FromSeconds(2), retryCount: 1, error: "The bank timed out");
        AutomationRun done = AutomationTextTests.Run(AutomationRunStatus.Done, Due.AddMinutes(-15));
        _automationService.GetRunsAsync(AutomationTextTests.SyncFunction, 0, 25, Arg.Any<CancellationToken>())
            .Returns(new AutomationRunPage([failed, done], 2));

        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Act
        await Button(cut, "Show runs").ClickAsync(new());
        IElement errorToggle = cut.WaitForElement("[aria-label='Show the error from 3 Nov 14:00']");
        string before = cut.Markup;
        await errorToggle.ClickAsync(new());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-run")).Count().IsEqualTo(2);
            await Assert.That(before).Contains("Failed");
            await Assert.That(before).Contains("Manual");
            await Assert.That(before).Contains("Retried once");
            await Assert.That(before).DoesNotContain("The bank timed out");
            await Assert.That(cut.Find(".ft-run__error").TextContent).IsEqualTo("The bank timed out");
        }
    }

    [Test]
    public async Task ShowRuns_WithMoreThanAPage_GoesToTheNextPage()
    {
        // Arrange
        Jobs(AutomationTextTests.Job());
        List<AutomationRun> firstPage = Enumerable.Range(0, 25)
            .Select(x => AutomationTextTests.Run(AutomationRunStatus.Done, Due.AddMinutes(-15 * x)))
            .ToList();
        AutomationRun oldest = AutomationTextTests.Run(AutomationRunStatus.Failed, Due.AddDays(-1));
        _automationService.GetRunsAsync(AutomationTextTests.SyncFunction, 0, 25, Arg.Any<CancellationToken>())
            .Returns(new AutomationRunPage(firstPage, 26));
        _automationService.GetRunsAsync(AutomationTextTests.SyncFunction, 1, 25, Arg.Any<CancellationToken>())
            .Returns(new AutomationRunPage([oldest], 26));

        IRenderedComponent<AutomationPage> cut = RenderPage();
        await Button(cut, "Show runs").ClickAsync(new());

        // Act
        await cut.WaitForElement(".mud-pagination").QuerySelectorAll("button")
            .Single(x => x.TextContent.Trim() == "2")
            .ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-run").Count == 1);
        await Assert.That(cut.Markup).Contains("Failed");
    }

    [Test]
    public async Task NoJobs_SaysSo()
    {
        // Arrange
        Jobs();

        // Act
        IRenderedComponent<AutomationPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("No jobs scheduled.");
    }

    private void Jobs(params AutomationJobSummary[] jobs) =>
        _automationService.GetJobSummariesAsync(Arg.Any<CancellationToken>()).Returns(jobs.ToAsyncEnumerable());

    private static IElement Button(IRenderedComponent<AutomationPage> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);

    private IRenderedComponent<AutomationPage> RenderPage()
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        return Render<AutomationPage>(p => p.AddCascadingValue(_applicationState));
    }
}
