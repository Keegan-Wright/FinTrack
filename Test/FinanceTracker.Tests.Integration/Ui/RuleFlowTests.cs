using FinanceTracker.Domain.Rules;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Rules, made and changed on their own page rather than in a dialog. A new one, one started from a transaction,
/// saving and applying it to what's there, editing, reordering and deleting.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class RuleFlowTests : UiTestBase
{
    private const string NewRule = "Tag the pay";

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task NewRule_FilledInAndSaved_IsListed(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/Rules");

        // Act
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "New rule", Exact = true })
            .ClickAsync();
        await WaitUntilLoadedAsync(page);
        await FillInAsync(page);
        List<string> problems = await AccessibilityProblemsAsync(page);
        List<string> tooWide = await WhatScrollsSidewaysAsync(page);
        await Button(page, "Save").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText($"{NewRule} saved")).ToBeVisibleAsync();
        await page.WaitForURLAsync("**/Rules");
        await Assertions.Expect(page.Locator(".ft-rule__name").Filter(new LocatorFilterOptions { HasText = NewRule }))
            .ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(problems).IsEmpty();
            await Assert.That(tooWide).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task NewRule_SavedAndApplied_ChangesTheTransactionsItMatches()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Rules/new");
        await WaitUntilLoadedAsync(page);
        await FillInAsync(page);

        // Act
        await Button(page, "Preview").ClickAsync();
        await Assertions.Expect(page.GetByText(SeededUser.Pay).First).ToBeVisibleAsync();
        await Button(page, "Save and apply").ClickAsync();
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Save and apply", Exact = true
        }).ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText($"{NewRule} saved and applied, 1 transaction changed"))
            .ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task CreateRuleFromATransaction_StartsFromWhatItSays()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);

        // Act
        await ChooseFromRowMenuAsync(page, SeededUser.Groceries, "Create rule from this");
        await page.WaitForURLAsync("**/Rules/new?from=*");
        await WaitUntilLoadedAsync(page);

        // Assert
        ILocator text = page.GetByLabel("Text", new PageGetByLabelOptions { Exact = true });
        await Assertions.Expect(text).ToHaveValueAsync(SeededUser.Groceries);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task EditRule_RenamedAndSaved_IsListedByItsNewName()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Rules");

        // Act
        await ChooseFromRowMenuAsync(page, SeededUser.Rule, "Edit");
        await WaitUntilLoadedAsync(page);
        await page.GetByLabel("Name", new PageGetByLabelOptions { Exact = true }).FillAsync(NewRule);
        await Button(page, "Save").ClickAsync();

        // Assert
        await page.WaitForURLAsync("**/Rules");
        await Assertions.Expect(page.Locator(".ft-rule__name")).ToHaveTextAsync([NewRule]);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task MoveDown_FromTheRowMenu_ChangesTheOrderTheyRunIn()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        const string second = "Runs second";
        await Scenario.Do(s => s.RuleScenario.WithRule(second, user.Username, userId => new Rule(userId, second, 1,
            RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains, SeededUser.Pay)],
            [RuleAction.MarkNotATransfer()])));
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Rules");
        ILocator names = page.Locator(".ft-rule__name");
        await Assertions.Expect(names).ToHaveTextAsync([SeededUser.Rule, second]);

        // Act, waiting for the new order to be saved, leaving the page sooner would cancel it
        await page.RunAndWaitForResponseAsync(() => ChooseFromRowMenuAsync(page, SeededUser.Rule, "Move down"),
            response => response.Request.Method == "PUT" && response.Url.EndsWith("/order", StringComparison.Ordinal));

        // Assert, kept once the page loads again
        await Assertions.Expect(names).ToHaveTextAsync([second, SeededUser.Rule]);
        await GoToAsync(page, "/Rules");
        await Assertions.Expect(names).ToHaveTextAsync([second, SeededUser.Rule]);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task DeleteRule_FromTheListOnceConfirmed_RemovesIt()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Rules");

        // Act
        await ChooseFromRowMenuAsync(page, SeededUser.Rule, "Delete");
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete", Exact = true })
            .ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator(".ft-rule__name")).ToHaveCountAsync(0);
        await GoToAsync(page, "/Rules");
        await Assertions.Expect(page.Locator(".ft-rule__name")).ToHaveCountAsync(0);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task DeleteRule_FromItsPageOnceConfirmed_RemovesIt()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, $"/Rules/{Scenario.RuleMappings[SeededUser.Rule]}");
        await WaitUntilLoadedAsync(page);

        // Act
        await Button(page, "Delete").ClickAsync();
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete", Exact = true })
            .ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText($"{SeededUser.Rule} deleted")).ToBeVisibleAsync();
        await page.WaitForURLAsync("**/Rules");
        await Assertions.Expect(page.Locator(".ft-rule__name")).ToHaveCountAsync(0);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    // A rule that tags the seeded pay with the seeded tag, which only the pay matches
    private static async Task FillInAsync(IPage page)
    {
        await page.GetByLabel("Name", new PageGetByLabelOptions { Exact = true }).FillAsync(NewRule);
        ILocator form = page.Locator("body");
        await ChooseOptionAsync(page, form, "Look at", "Description");
        await page.GetByLabel("Text", new PageGetByLabelOptions { Exact = true }).FillAsync(SeededUser.Pay);
        await ChooseOptionAsync(page, form, "Tag", SeededUser.Tag);
    }
}
