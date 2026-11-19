using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Making a budget through its dialog, on tags or an account, which has to fit and work on a phone as well as a
/// desktop.
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
        await dialog.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = "Tags", Exact = true }).ClickAsync();
        await page.Locator(".mud-popover-open .mud-list-item")
            .Filter(new LocatorFilterOptions { HasText = "Food shop" })
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

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task NewAccountBudget_CountingMoneyMovedOut_SaysWhatItCovers(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("joint", "bank", static (userId, connectionId) =>
                new BankAccount(userId, connectionId, "ext-joint", "TRANSACTION", "Joint", "GBP")));
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/Budgets");

        // Act
        await Button(page, "New budget").ClickAsync();
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByLabel("Name").FillAsync("Joint account");
        await dialog.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = "Accounts", Exact = true })
            .ClickAsync();
        await page.Locator(".mud-popover-open .mud-list-item")
            .Filter(new LocatorFilterOptions { HasText = "Joint" })
            .ClickAsync();
        await page.Keyboard.PressAsync("Escape");
        await dialog.GetByLabel("Monthly limit").FillAsync("1500");
        await dialog.GetByLabel("Count money moved out to other accounts").CheckAsync();
        List<string> dialogProblems = await AccessibilityProblemsAsync(page);
        List<string> dialogTooWide = await WhatScrollsSidewaysAsync(page);
        await Button(page, "OK").ClickAsync();

        // Assert
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(page.GetByText("All spending and money moved out of Joint")).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(dialogProblems).IsEmpty();
            await Assert.That(dialogTooWide).IsEmpty();
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }
}
