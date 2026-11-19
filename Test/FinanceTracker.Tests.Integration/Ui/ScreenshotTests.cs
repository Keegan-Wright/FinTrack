using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Takes the README's screenshots, of six months of made up data (<see cref="ScreenshotUser"/>), and the docs' own.
/// Explicit, so it only runs when asked for.
/// <code>
/// dotnet test --project Test/FinanceTracker.Tests.Integration -- --treenode-filter '/*/*/ScreenshotTests/*'
/// </code>
/// The images are written to <c>Docs/images</c>.
/// </summary>
[Explicit]
[ParallelLimiter<UiParallelLimit>]
public class ScreenshotTests : UiTestBase
{
    private const string ImagesFolder = "Docs/images";
    private const string RuleId = "{rule}";
    private const string LinkId = "{link}";

    /// <summary>
    /// Each page, and the name its images are saved under.
    /// </summary>
    private static readonly (string Path, string Name)[] Pages =
    [
        ("/", "dashboard"),
        ("/Accounts", "accounts"),
        ("/Transactions", "transactions"),
        ("/Bills", "bills"),
        ("/NetWorth", "net-worth"),
        ($"/Rules/{RuleId}", "rule"),
        ($"/Sharing/{LinkId}", "sharing")
    ];

    public static IEnumerable<(string Path, string Name, ScreenSize Screen)> PagesOnEachScreen()
    {
        foreach ((string path, string name) in Pages)
        {
            yield return (path, name, ScreenSize.Desktop);
            yield return (path, name, ScreenSize.Phone);
        }
    }

    [Test]
    [MethodDataSource(nameof(PagesOnEachScreen))]
    public async Task Page_IsCaptured(string path, string name, ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(firstName: ScreenshotUser.FirstName);
        await ScreenshotUser.SeedAsync(Scenario, user.Username, NowFor(user), DbContextFactory);
        if (path.Contains(LinkId, StringComparison.Ordinal))
        {
            Guid linkId = await ShareWithAPartnerAsync(user, screen);
            path = path.Replace(LinkId, linkId.ToString(), StringComparison.Ordinal);
        }

        path = path.Replace(RuleId, Scenario.RuleMappings[ScreenshotUser.RuleName].ToString(),
            StringComparison.Ordinal);
        IPage page = await OpenAsync(screen, user, false);

        // Act
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        await page.Locator(".ft-page-header__title").WaitForAsync();

        // Charts animate in
        await page.WaitForTimeoutAsync(1500);

        // Toasts from work the setup set off, e.g. sharing refreshing the accounts, aren't part of the page
        await page.EvaluateAsync("() => document.querySelectorAll('.mud-snackbar').forEach(x => x.remove())");

        string file = Path.Combine(RepositoryRoot(), ImagesFolder, $"{name}-{screen.Name.ToLowerInvariant()}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = file });

        // Assert
        await Assert.That(File.Exists(file)).IsTrue();
    }

    public static IEnumerable<ScreenSize> DesktopAndPhone()
    {
        yield return ScreenSize.Desktop;
        yield return ScreenSize.Phone;
    }

    [Test]
    [MethodDataSource(nameof(DesktopAndPhone))]
    public async Task TwoFactorSetup_IsCaptured(ScreenSize screen)
    {
        // Arrange, for the two-factor docs. The key and QR code are a throwaway user's, named per screen as
        // usernames are unique
        string username = $"{ScreenshotUser.FirstName}.{screen.Name}".ToLowerInvariant();
        SignedInUser user = await RegisterUserAsync(username, firstName: ScreenshotUser.FirstName);
        IPage page = await OpenAsync(screen, user, false);
        await GoToAsync(page, "/YourProfile");
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Two-factor" }).ClickAsync();

        // Act
        await Button(page, "Set up two-factor sign in").ClickAsync();
        await page.Locator(".ft-two-factor__key").WaitForAsync();

        // The tab's underline slides across
        await page.WaitForTimeoutAsync(1000);
        string file = Path.Combine(RepositoryRoot(), ImagesFolder,
            $"two-factor-setup-{screen.Name.ToLowerInvariant()}.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = file });

        // Assert
        await Assert.That(File.Exists(file)).IsTrue();
    }

    /// <summary>
    /// Links <paramref name="user"/> with a partner, who accepts, and shares the bills account with them.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="screen">The screen, which tells each run's partner apart, as usernames are unique.</param>
    /// <returns>The link.</returns>
    private async Task<Guid> ShareWithAPartnerAsync(SignedInUser user, ScreenSize screen)
    {
        Bogus.Person person = ScreenshotUser.Partner;
        string username = $"{person.FirstName}.{person.LastName}".ToLowerInvariant();
        if (screen != ScreenSize.Desktop)
        {
            username = $"{username}.{screen.Name}".ToLowerInvariant();
        }

        SignedInUser partner = await RegisterUserAsync(username, firstName: person.FirstName,
            lastName: person.LastName);
        ISharingService sharing = ApiFor(user).Get<ISharingService>();

        InviteOutcome invite = await sharing.InviteAsync(new InviteToShare { Who = partner.Username },
            CancellationToken);
        Guid linkId = invite.Link!.Id;
        await ApiFor(partner).Get<ISharingService>().AcceptAsync(linkId, CancellationToken);

        AccountShareChoice billsAccount = new()
        {
            AccountId = Scenario.AccountMappings[ScreenshotUser.BillsAccount],
            Mode = ShareMode.AllowChanges
        };
        await sharing.ChangeAccountSharesAsync(linkId, new ChangeAccountShares { Accounts = [billsAccount] },
            CancellationToken);
        return linkId;
    }

    /// <summary>
    /// The repository's root, the first folder above the tests with the solution in it.
    /// </summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FinanceTracker.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("The repository root wasn't found.");
    }
}
