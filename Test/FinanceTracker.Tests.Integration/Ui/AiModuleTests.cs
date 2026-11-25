#if FINTRACK_AI
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Where the AI module meets the app, in the AI build only. The module's CI jobs run these by their category, the
/// core's own suites run without the module built in.
/// </summary>
[Category("AiModule")]
[ParallelLimiter<UiParallelLimit>]
public class AiModuleTests : UiTestBase
{
    [Test]
    public async Task AppBarButton_OpensTheAiPageWithoutReloading()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/");
        await WaitUntilInteractiveAsync(page);
        // Marks this page load, a full reload would lose it
        await page.EvaluateAsync("window.ftSameLoad = true");

        // Act
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Open AI" }).ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("AI");
        using (Assert.Multiple())
        {
            await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/Ai");
            await Assert.That(await page.EvaluateAsync<bool>("window.ftSameLoad === true")).IsTrue();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task AiPage_OpenedDirectly_RendersAndStaysOnScreen(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);

        // Act, the server renders it, then the browser loads the module before taking over
        await GoToAsync(page, "/Ai");
        await WaitUntilInteractiveAsync(page);

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("AI");
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    [Arguments("/", null)]
    [Arguments("/Transactions", "Transactions")]
    [Arguments("/YourProfile", "Your profile")]
    public async Task CorePages_WithTheModuleBuiltIn_StillRender(string path, string? title)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);

        // Act
        await GoToAsync(page, path);
        await WaitUntilInteractiveAsync(page);

        // Assert
        if (title is not null)
        {
            await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync(title);
        }

        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task Startup_AppliesTheModulesMigrationInItsOwnSchema()
    {
        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string> tables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'ai'")
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(tables).IsEquivalentTo(["Settings", "__AiMigrationsHistory"]);
    }
}
#endif
