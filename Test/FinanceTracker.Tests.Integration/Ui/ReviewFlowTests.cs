using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// The two review pages, empty and with something to decide, and deciding it: transfers FinTrack isn't sure of, and
/// repeat payments that might be subscriptions.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class ReviewFlowTests : UiTestBase
{
    private const string TransfersCaughtUp = "All caught up. FinTrack asks here when it isn't sure";
    private const string SubscriptionsCaughtUp = "All caught up. FinTrack asks here when the same payment";

    [Test]
    [Arguments("/Review", TransfersCaughtUp)]
    [Arguments("/Review/Subscriptions", SubscriptionsCaughtUp)]
    public async Task ReviewPage_WithNothingToReview_SaysAllCaughtUp(string path, string caughtUp)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);

        // Act
        await GoToAsync(page, path);

        // Assert
        await Assertions.Expect(page.GetByText(caughtUp)).ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task ReviewTransfers_WithOneToReview_ShowsItAndWhatToDecide(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(screen, user);

        // Act
        await GoToAsync(page, "/Review");

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header")).ToContainTextAsync("1 to review");
        ILocator row = page.Locator(".ft-review");
        using (Assert.Multiple())
        {
            await Assert.That(await row.CountAsync()).IsEqualTo(1);
            await Assert.That(await row.InnerTextAsync()).Contains(SeededUser.ToReview)
                .And.Contains(SeededUser.Current);
            await Assert.That(await row.GetByRole(AriaRole.Button).AllInnerTextsAsync())
                .IsEquivalentTo(["It's a transfer", "Not a transfer"]);
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    /// <summary>
    /// Either answer takes it off the list, and the transaction's chip in the grid shows the answer.
    /// </summary>
    [Test]
    [Arguments("It's a transfer", "Transfer")]
    [Arguments("Not a transfer", "Not a transfer")]
    public async Task ReviewTransfers_Decided_LeavesTheListAndShowsOnTheTransaction(string answer, string chip)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Review");

        // Act
        await page.Locator(".ft-review").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = answer, Exact = true
        }).ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText(TransfersCaughtUp)).ToBeVisibleAsync();
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);
        ILocator transaction = page.GetByRole(AriaRole.Row).Filter(new LocatorFilterOptions
        {
            HasText = SeededUser.ToReview
        });
        await Assertions.Expect(transaction.Locator(".ft-transfer-chip")).ToHaveTextAsync(chip);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task ReviewSubscriptions_WithOneToReview_ShowsItAndWhatToDecide(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(screen, user);

        // Act
        await GoToAsync(page, "/Review/Subscriptions");

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header")).ToContainTextAsync("1 to review");
        ILocator row = page.Locator(".ft-list__row").Filter(new LocatorFilterOptions { HasText = SeededUser.Phone });
        using (Assert.Multiple())
        {
            await Assert.That(await row.CountAsync()).IsEqualTo(1);
            await Assert.That(await row.GetByRole(AriaRole.Button).AllInnerTextsAsync())
                .IsEquivalentTo(["It's a bill", "Not a bill"]);
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task ReviewSubscriptions_ItsABill_LeavesTheListAndIsABill()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Review/Subscriptions");

        // Act
        await Button(page, "It's a bill").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText($"{SeededUser.Phone} is a bill now")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(SubscriptionsCaughtUp)).ToBeVisibleAsync();
        await GoToAsync(page, "/Bills");
        await Assertions.Expect(page.GetByText(SeededUser.Phone).First).ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task ReviewSubscriptions_NotABill_LeavesTheListAndIsntSuggestedAgain()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Review/Subscriptions");

        // Act
        await Button(page, "Not a bill").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText($"{SeededUser.Phone} won't be suggested again")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(SubscriptionsCaughtUp)).ToBeVisibleAsync();
        await GoToAsync(page, "/Review/Subscriptions");
        await Assertions.Expect(page.GetByText(SubscriptionsCaughtUp)).ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }
}
