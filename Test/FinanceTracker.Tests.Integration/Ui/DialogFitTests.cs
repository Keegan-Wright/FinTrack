using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// The dialogs a page opens to add something, on a phone, the smallest screen they have to fit: all of each dialog
/// is on the screen, its buttons included, and axe finds nothing in it. The budget dialog is filled in and saved
/// on each screen in <see cref="BudgetFlowTests"/>.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class DialogFitTests : UiTestBase
{
    public static IEnumerable<(string Path, string Button)> AddDialogs() =>
    [
        ("/Bills", "New bill"),
        ("/Debts", "New debt"),
        ("/SavingsGoals", "New goal"),
        ("/Classifications", "New tag"),
        ("/HouseholdMembers", "Add person"),
        ("/Calendar", "Add event")
    ];

    [Test]
    [MethodDataSource(nameof(AddDialogs))]
    public async Task AddDialog_OnAPhone_FitsTheScreenAndIsAccessible(string path, string button)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);

        // Act
        await Button(page, button).ClickAsync();
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await FitsOnScreenAsync(page, dialog)).IsTrue();
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }
}
