using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// The files the user can take away: a report as a PDF from the reports page, and a statement and the backup from
/// Your data, each saved by the browser.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class ExportFlowTests : UiTestBase
{
    [Test]
    public async Task Report_ByTag_ExportsAsAPdf()
    {
        // Arrange
        SignedInUser user = await WithATransactionAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/Reports");

        // Act
        await page.GetByText("By tag", new PageGetByTextOptions { Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Reports/tags$"));
        await WaitUntilLoadedAsync(page);
        await Assertions.Expect(Button(page, "Export PDF")).ToBeEnabledAsync();
        IDownload pdf = await DownloadAsync(page, () => Button(page, "Export PDF").ClickAsync());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pdf.SuggestedFilename).StartsWith("fintrack-report-");
            await Assert.That(await StartOfAsync(pdf, 5)).IsEqualTo("%PDF-");
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task YourData_StatementAndBackup_AreSaved()
    {
        // Arrange
        SignedInUser user = await WithATransactionAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/YourData");

        // Act
        IDownload statement = await DownloadAsync(page, () => Button(page, "Download statement").ClickAsync());
        IDownload backup = await DownloadAsync(page, () => Button(page, "Download backup").ClickAsync());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(statement.SuggestedFilename).StartsWith("fintrack-statement-");
            await Assert.That(await StartOfAsync(statement, 5)).IsEqualTo("%PDF-");
            await Assert.That(backup.SuggestedFilename).StartsWith("fintrack-backup-");
            await Assert.That(await StartOfAsync(backup, 2)).IsEqualTo("PK");
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    private async Task<SignedInUser> WithATransactionAsync()
    {
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main",
                // A bank classification of its own, the tag report has nothing to export without a tag
                new TransactionDetailsFaker().Generate() with
                {
                    Description = "COFFEE SHOP", IsPending = false, OccurredAt = NowFor(user).AddDays(-1),
                    ProviderClassifications = ["Entertainment"]
                }));
        return user;
    }
}
