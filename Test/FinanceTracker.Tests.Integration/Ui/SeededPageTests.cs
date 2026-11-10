using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Every page for a user with something of everything (<see cref="SeededUser"/>), on each screen in light and on a
/// desktop in dark. It shows what it has, starts without errors, fits the screen, and axe finds nothing against
/// WCAG 2.1 AA. <see cref="PageRenderTests"/> checks the same pages empty, rows, charts and chips only show up here.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class SeededPageTests : UiTestBase
{
    private const string DebtId = "{debt}";
    private const string RuleId = "{rule}";

    /// <summary>
    /// Each page and something of the seeded user's it shows once loaded, null where there's nothing by name to
    /// look for, e.g. a chart. <see cref="DebtId"/> and <see cref="RuleId"/> stand in for the seeded debt and rule.
    /// </summary>
    private static readonly (string Path, string? Shows)[] Pages =
    [
        ("/", null),
        ("/Accounts", SeededUser.Current),
        ("/Transactions", SeededUser.Groceries),
        ("/Calendar", SeededUser.Event),
        ("/Budgets", SeededUser.Budget),
        ("/Bills", SeededUser.Bill),
        ("/Debts", SeededUser.Debt),
        ($"/Debts/{DebtId}", SeededUser.Debt),
        ("/SavingsGoals", SeededUser.Goal),
        ("/NetWorth", null),
        ("/Reports", null),
        ("/Reports/accounts", SeededUser.Current),
        ("/Reports/tags", SeededUser.Tag),
        ("/Review", SeededUser.ToReview),
        ("/Review/Subscriptions", SeededUser.Phone),
        ("/Classifications", SeededUser.Tag),
        ("/Rules", SeededUser.Rule),
        ($"/Rules/{RuleId}", null),
        ("/HouseholdMembers", $"{SeededUser.MemberFirstName} {SeededUser.MemberLastName}"),
        ("/ProviderSetup", null),
        ("/Notifications", SeededUser.Notice),
        ("/Notifications/Settings", "What and where"),
        ("/YourData", null),
        ("/YourProfile", "Change email")
    ];

    public static IEnumerable<(string Path, string? Shows, ScreenSize Screen, bool Dark)> PagesOnEachScreen()
    {
        foreach ((string path, string? shows) in Pages)
        {
            foreach (ScreenSize screen in ScreenSize.All)
            {
                yield return (path, shows, screen, false);
            }

            yield return (path, shows, ScreenSize.Desktop, true);
        }
    }

    [Test]
    [MethodDataSource(nameof(PagesOnEachScreen))]
    public async Task Page_WithData_ShowsItFitsTheScreenAndIsAccessible(string path, string? shows,
        ScreenSize screen, bool dark)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(screen, user, dark);

        // Act
        await GoToAsync(page, WithSeededIds(path));
        await WaitUntilLoadedAsync(page);

        // Assert
        await page.Locator(".ft-page-header__title").WaitForAsync();
        if (shows is not null)
        {
            await Assertions.Expect(page.GetByText(shows).First).ToBeVisibleAsync();
        }

        using (Assert.Multiple())
        {
            await Assert.That(BrowserErrors).IsEmpty();
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
        }
    }

    private string WithSeededIds(string path) => path
        .Replace(DebtId, Scenario.DebtMappings[SeededUser.Debt].ToString(), StringComparison.Ordinal)
        .Replace(RuleId, Scenario.RuleMappings[SeededUser.Rule].ToString(), StringComparison.Ordinal);
}
