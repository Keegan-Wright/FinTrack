using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Making a budget through its dialog, which has to fit and work on a phone as well as a desktop.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class BudgetFlowTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task NewBudget_FilledInAndSaved_IsListed(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.ClassificationScenario.WithClassification("food", user.Username, "Food shop"));
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/Budgets");

        // Act
        await Button(page, "New budget").ClickAsync();
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByLabel("Name").FillAsync("Weekly shop");
        await dialog.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = "Tags it covers" }).ClickAsync();
        await page.Locator(".mud-popover-open .mud-list-item").Filter(new LocatorFilterOptions { HasText = "Food shop" })
            .ClickAsync();
        await page.Keyboard.PressAsync("Escape");
        await dialog.GetByLabel("Monthly limit").FillAsync("400");
        List<string> dialogProblems = await AccessibilityProblemsAsync(page);
        List<string> dialogTooWide = await WhatScrollsSidewaysAsync(page);
        await Button(page, "OK").ClickAsync();

        // Assert
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(page.GetByText("Weekly shop").First).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(dialogProblems).IsEmpty();
            await Assert.That(dialogTooWide).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }
}
