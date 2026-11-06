using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Finding, tagging and exporting transactions on the transactions page, as a list on a phone and a table from a
/// tablet up.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class TransactionFlowTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Search_OnceApplied_ShowsOnlyWhatMatches(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await WithCoffeeAndRentAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/Transactions");

        // Act
        await page.GetByPlaceholder("Search descriptions").FillAsync("coffee");
        await Button(page, "Apply").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText("COFFEE SHOP").First).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(await page.GetByText("RENT", new PageGetByTextOptions { Exact = true }).CountAsync())
                .IsEqualTo(0);
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task ExportCsv_DownloadsWhatTheFiltersShow()
    {
        // Arrange
        SignedInUser user = await WithCoffeeAndRentAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");
        await page.GetByPlaceholder("Search descriptions").FillAsync("coffee");
        await Button(page, "Apply").ClickAsync();
        await Assertions.Expect(page.GetByText("COFFEE SHOP").First).ToBeVisibleAsync();

        // Act
        IDownload csv = await DownloadAsync(page, () => Button(page, "Export CSV").ClickAsync());

        // Assert
        string text = await TextOfAsync(csv);
        using (Assert.Multiple())
        {
            await Assert.That(csv.SuggestedFilename).StartsWith("fintrack-transactions-");
            await Assert.That(text).Contains("COFFEE SHOP");
            await Assert.That(text).DoesNotContain("RENT");
        }
    }

    [Test]
    public async Task AddTags_ChosenInTheDialog_ShowOnTheTransaction()
    {
        // Arrange
        SignedInUser user = await WithCoffeeAndRentAsync();
        string tag = $"Treats {Guid.NewGuid():N}"[..14];
        await Scenario.Do(s => s.ClassificationScenario.WithClassification("treats", user.Username, tag));
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Transactions");

        // Act
        await ChooseFromRowMenuAsync(page, "COFFEE SHOP", "Add tags");
        await Assertions.Expect(Dialog(page)).ToBeVisibleAsync();
        List<string> dialogProblems = await AccessibilityProblemsAsync(page);
        await Dialog(page).GetByLabel(tag).CheckAsync();
        await Button(page, "OK").ClickAsync();

        // Assert
        await Assertions.Expect(Dialog(page)).ToBeHiddenAsync();
        await Assertions.Expect(page.GetByText(tag, new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(dialogProblems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    /// <summary>
    /// A user with a coffee and their rent in one account.
    /// </summary>
    private async Task<SignedInUser> WithCoffeeAndRentAsync()
    {
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main",
                new TransactionDetailsFaker().Generate() with { Description = "COFFEE SHOP", IsPending = false }))
            .Do(s => s.TransactionScenario.WithTransaction("rent", "main",
                new TransactionDetailsFaker().Generate() with { Description = "RENT", IsPending = false }));
        return user;
    }
}
