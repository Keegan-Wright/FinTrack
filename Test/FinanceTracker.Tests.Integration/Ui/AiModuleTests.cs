#if FINTRACK_AI
using FinanceTracker.Ai.Contracts;
using FinanceTracker.Client.Ai;
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
    /// <summary>
    /// The Ollama address every test sets, the same each time as the install's settings are shared. Nothing is
    /// called on it yet.
    /// </summary>
    private const string OllamaAddress = "http://ollama.example.test:11434";

    [Test]
    public async Task AppBarButton_UserHasTurnedAiOn_OpensTheAiPageWithoutReloading()
    {
        // Arrange
        await SetUpAiAsync();
        SignedInUser user = await RegisterUserAsync();
        await Ai(user).ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);
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
        await SetUpAiAsync();
        SignedInUser user = await RegisterUserAsync();
        await Ai(user).ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);
        IPage page = await OpenAsync(screen, user);

        // Act, the server renders it, then the browser loads the module before taking over
        await GoToAsync(page, "/Ai");
        await WaitUntilInteractiveAsync(page);

        // Assert
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync("AI");
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task ProfileSwitch_TurningAiOn_ShowsItInTheMenu()
    {
        // Arrange
        await SetUpAiAsync();
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/YourProfile");
        await WaitUntilInteractiveAsync(page);
        ILocator aiInMenu = page.Locator("#nav-drawer")
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "AI", Exact = true });
        await Assertions.Expect(aiInMenu).ToHaveCountAsync(0);

        // Act
        await page.GetByLabel("Use AI").ClickAsync();

        // Assert
        await Assertions.Expect(aiInMenu).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That((await Ai(user).GetStatusAsync(CancellationToken)).OptedIn).IsTrue();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task SettingsPage_AdminSavesAnAddress_KeepsIt()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, admin);
        await GoToAsync(page, "/Ai/Settings");
        await WaitUntilInteractiveAsync(page);

        // Act
        await page.GetByLabel("Ollama address").FillAsync(OllamaAddress);
        await Button(page, "Save").ClickAsync();
        await Assertions.Expect(page.GetByText("AI settings saved")).ToBeVisibleAsync();
        await page.ReloadAsync();
        await WaitUntilInteractiveAsync(page);

        // Assert
        await Assertions.Expect(page.GetByLabel("Ollama address")).ToHaveValueAsync(OllamaAddress);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task SaveSettings_AddressThatIsNotHttp_IsRefused()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();

        // Act
        Func<Task> save = () => Ai(admin)
            .SaveSettingsAsync(new SaveAiSettings { OllamaEndpoint = "ftp://ollama:11434" }, CancellationToken);

        // Assert
        HttpRequestException? refused = await Assert.That(save).Throws<HttpRequestException>();
        await Assert.That(refused!.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
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
    public async Task Startup_AppliesTheModulesMigrationsInItsOwnSchema()
    {
        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string> tables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'ai'")
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(tables).IsEquivalentTo(["Settings", "UserSettings", "__AiMigrationsHistory"]);
    }

    /// <summary>
    /// The AI API as <paramref name="user"/>, through the browser's own client.
    /// </summary>
    private AiApiClient Ai(SignedInUser user) => new(ApiFor(user).Get<IHttpClientFactory>());

    /// <summary>
    /// Sets AI up for the install, as an admin would.
    /// </summary>
    private async Task SetUpAiAsync()
    {
        SignedInUser admin = await RegisterAdminAsync();
        await Ai(admin).SaveSettingsAsync(new SaveAiSettings { OllamaEndpoint = OllamaAddress }, CancellationToken);
    }
}
#endif
