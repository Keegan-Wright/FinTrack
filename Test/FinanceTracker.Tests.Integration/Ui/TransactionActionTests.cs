using System.Text.RegularExpressions;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// What can be done to a transaction from its ⋮ menu, its details and its transfer chip, and finding the ones
/// wanted with the filters and pages. Search, Export CSV and Add tags are in <see cref="TransactionFlowTests"/>.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public partial class TransactionActionTests : UiTestBase
{
    [Test]
    public async Task Details_TappedOnAPhone_OpenWithWhatCanBeDone()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);

        // Act
        await page.Locator(".ft-transaction").Filter(new LocatorFilterOptions { HasText = SeededUser.Groceries })
            .ClickAsync();

        // Assert
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToContainTextAsync(SeededUser.Groceries);
        using (Assert.Multiple())
        {
            await Assert.That(await dialog.GetByRole(AriaRole.Button).AllInnerTextsAsync())
                .Contains("Pays a bill").And.Contains("Pays off a debt").And.Contains("Create rule from this");
            await Assert.That(await FitsOnScreenAsync(page, dialog)).IsTrue();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task Details_FromTheRowMenu_Open()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);

        // Act
        await ChooseFromRowMenuAsync(page, SeededUser.Groceries, "Details and bank classifications");

        // Assert
        await Assertions.Expect(Dialog(page)).ToContainTextAsync(SeededUser.Groceries);
        await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
    }

    [Test]
    public async Task Split_IntoTwoPartsThenRemoved_CountsAsOneAgain()
    {
        // Arrange, the groceries are £42.18
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);
        await page.Locator(".ft-transaction").Filter(new LocatorFilterOptions { HasText = SeededUser.Groceries })
            .ClickAsync();

        // Act, split
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Split", Exact = true })
            .ClickAsync();
        ILocator split = page.Locator(".mud-dialog").Filter(new LocatorFilterOptions { Has = page.Locator(".ft-split__remaining") });
        ILocator amounts = split.GetByLabel("Amount", new LocatorGetByLabelOptions { Exact = true });
        await amounts.Nth(0).FillAsync("30");
        await amounts.Nth(1).FillAsync("12.18");
        bool fits = await FitsOnScreenAsync(page, split);
        List<string> problems = await AccessibilityProblemsAsync(page);
        await split.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Save", Exact = true })
            .ClickAsync();

        // Assert, split
        await Assertions.Expect(page.GetByText("Split into 2 parts")).ToBeVisibleAsync();

        // Act, remove the split from its details
        await page.Locator(".ft-transaction").Filter(new LocatorFilterOptions { HasText = SeededUser.Groceries })
            .ClickAsync();
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Remove split", Exact = true
        }).ClickAsync();
        await page.Locator(".mud-dialog").Last.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Remove split", Exact = true
        }).ClickAsync();

        // Assert, one again
        await Assertions.Expect(page.GetByText("Split removed")).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(fits).IsTrue();
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    [Arguments("Pays a bill", "Pays", SeededUser.Bill, $"It pays {SeededUser.Bill} now")]
    [Arguments("Pays off a debt", "Pays off", SeededUser.Debt, $"It pays off {SeededUser.Debt} now")]
    public async Task Pays_ChosenFromTheRowMenu_LinksTheTransaction(string item, string select, string option,
        string said)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);

        // Act
        await ChooseFromRowMenuAsync(page, SeededUser.Groceries, item);
        ILocator dialog = Dialog(page);
        await ChooseOptionAsync(page, dialog, select, option);
        List<string> problems = await AccessibilityProblemsAsync(page);
        await Button(page, "OK").ClickAsync();

        // Assert, said and shown on the row
        await Assertions.Expect(page.GetByText(said)).ToBeVisibleAsync();
        ILocator row = page.GetByRole(AriaRole.Row).Filter(new LocatorFilterOptions { HasText = SeededUser.Groceries });
        await Assertions.Expect(row).ToContainTextAsync(option);
        using (Assert.Multiple())
        {
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task TransferChip_NotATransferChosen_CountsAsSpendingAgain()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);
        ILocator chip = page.GetByRole(AriaRole.Row).Filter(new LocatorFilterOptions { HasText = SeededUser.ToSavings })
            .Locator(".ft-transfer-chip");

        // Act
        await chip.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Not a transfer", Exact = true })
            .ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText("It counts as income or spending now")).ToBeVisibleAsync();
        await Assertions.Expect(chip).ToHaveTextAsync("Not a transfer");
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task TransferChip_FromTheKeyboard_OpensItsMenu()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);
        ILocator activator = page.GetByRole(AriaRole.Row).Filter(new LocatorFilterOptions { HasText = SeededUser.ToSavings })
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Transfer", Exact = true });

        // Act
        await activator.FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        // Assert
        await Assertions.Expect(page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions
        {
            Name = "Not a transfer", Exact = true
        })).ToBeVisibleAsync();
    }

    [Test]
    [Arguments("Accounts", SeededUser.Savings, SeededUser.FromCurrent)]
    [Arguments("Tags", SeededUser.Tag, SeededUser.Groceries)]
    public async Task MoreFilters_OneChosenAndApplied_ShowsOnlyWhatMatches(string filter, string option,
        string onlyMatch)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);

        // Act
        await Button(page, "More filters").ClickAsync();
        await TickOptionAsync(page, page.Locator(".ft-filters"), filter, option);
        await page.Keyboard.PressAsync("Escape");
        // Applied only once the select counts the choice, e.g. "1 tag", Apply reads what the filters hold
        await Assertions.Expect(page.Locator(".ft-filters").GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions
        {
            Name = filter
        })).ToHaveValueAsync(OneChosen());
        await Button(page, "Apply").ClickAsync();

        // Assert
        ILocator rows = page.Locator(".ft-transaction");
        await Assertions.Expect(rows).ToHaveCountAsync(1);
        await Assertions.Expect(rows).ToContainTextAsync(onlyMatch);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task ShowMore_OnAPhone_ShowsTheRest()
    {
        // Arrange, more than the list's 50 to a go
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);
        ILocator rows = page.Locator(".ft-transaction");
        await Assertions.Expect(rows).ToHaveCountAsync(50);

        // Act
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = ShowMore() }).ClickAsync();

        // Assert
        await Assertions.Expect(rows).ToHaveCountAsync(SeededTransactionCount);
    }

    [Test]
    public async Task NextPage_OnADesktop_ShowsTheNextTransactions()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");
        await WaitUntilLoadedAsync(page);
        await Assertions.Expect(page.GetByText($"1-50 of {SeededTransactionCount}")).ToBeVisibleAsync();

        // Act
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Next page" }).ClickAsync();

        // Assert, the grid's 50 to a page, the newest on the first
        await Assertions.Expect(page.GetByText($"51-{SeededTransactionCount} of {SeededTransactionCount}"))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(SeededUser.Pay)).ToHaveCountAsync(0);
    }

    // The named ones, the three phone payments and the made-up ones
    private const int SeededTransactionCount = 7 + 3 + SeededUser.ExtraTransactions;

    [GeneratedRegex(@"^Show \d+ more$")]
    private static partial Regex ShowMore();

    [GeneratedRegex("^1 ")]
    private static partial Regex OneChosen();
}
