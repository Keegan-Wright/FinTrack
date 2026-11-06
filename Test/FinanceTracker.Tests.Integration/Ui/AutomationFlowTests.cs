using FinanceTracker.Tests.Integration.Api;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// The Automation page, which only admins can see, so it isn't in <see cref="PageRenderTests"/>: it fits and is
/// accessible, and a job can be run from it.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class AutomationFlowTests : UiTestBase
{
    private const string MoveGoals = "Move event savings goals on";

    /// <summary>
    /// Always the real clock, even when the run is on a day of its own (<c>FINTRACK_TESTS_NOW</c>): the page shows
    /// the scheduler's runs, which happen on the real clock, and their history looks back from now.
    /// </summary>
    protected override DateTimeOffset? DefaultClock => null;

    [Test]
    [MethodDataSource(nameof(EachScreenInEachMode))]
    public async Task Page_AsAnAdmin_FitsTheScreenAndIsAccessible(ScreenSize screen, bool dark)
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IPage page = await OpenAsync(screen, admin, dark);

        // Act
        await GoToAsync(page, "/Automation");

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("Automation");
        await Assertions.Expect(page.GetByText(MoveGoals, new PageGetByTextOptions { Exact = true }))
            .ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(BrowserErrors).IsEmpty();
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
        }
    }

    public static IEnumerable<(ScreenSize Screen, bool Dark)> EachScreenInEachMode() =>
        ScreenSize.All.SelectMany(static screen => new[] { (screen, false), (screen, true) });

    /// <summary>
    /// Not alongside the API's run of the same job, a second run while one is going is turned away.
    /// </summary>
    [Test]
    [NotInParallel(AutomationApiTests.MoveGoalsFunction)]
    public async Task RunNow_Confirmed_RunsTheJobAndShowsTheRun()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, admin);
        await GoToAsync(page, "/Automation");

        // Act
        await ChooseFromRowMenuAsync(page, MoveGoals, "Run now");
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        bool dialogFits = await FitsOnScreenAsync(page, dialog);
        List<string> dialogProblems = await AccessibilityProblemsAsync(page);
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Run now", Exact = true })
            .ClickAsync();

        // Assert, the newest run is this one, done once the page hears it finished
        await Assertions.Expect(page.GetByText($"{MoveGoals} queued")).ToBeVisibleAsync();
        ILocator newestRun = page.Locator(".ft-job__history .ft-run").First;
        await Assertions.Expect(newestRun).ToContainTextAsync("Manual");
        await Assertions.Expect(newestRun.Locator(".ft-run-status")).ToHaveTextAsync("Done",
            new LocatorAssertionsToHaveTextOptions { Timeout = (float)TestTimeouts.Job.TotalMilliseconds });
        using (Assert.Multiple())
        {
            await Assert.That(dialogFits).IsTrue();
            await Assert.That(dialogProblems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }
}
