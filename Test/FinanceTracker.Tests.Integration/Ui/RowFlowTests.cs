using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Adding, editing and deleting on each page that lists the user's own things, as someone does on a phone. Each
/// dialog has to fit the screen and pass axe, and what's saved shows on the page. Every list's row has a ⋮ menu
/// labelled <c>Actions for {name}</c> with Edit and Delete, so the same steps cover them all. When the server fails
/// to save, the page says so and nothing changes.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class RowFlowTests : UiTestBase
{
    private const string NewName = "Renamed in a test";

    /// <summary>
    /// Each list, the seeded row on it, the menu item that edits it, and the field its dialog names it with.
    /// </summary>
    public static IEnumerable<(string Path, string Name, string EditItem, string NameField)> Rows() =>
    [
        ("/Bills", SeededUser.Bill, "Edit", "Name"),
        ("/Budgets", SeededUser.Budget, "Edit", "Name"),
        ("/Debts", SeededUser.Debt, "Edit", "Name"),
        ("/SavingsGoals", SeededUser.Goal, "Edit", "Name"),
        ("/Classifications", SeededUser.Tag, "Rename", "Tag"),
        ("/HouseholdMembers", $"{SeededUser.MemberFirstName} {SeededUser.MemberLastName}", "Edit", "First name"),
        ("/Calendar", SeededUser.Event, "Edit", "Title")
    ];

    /// <summary>
    /// Each list's add button, what a new one needs filled in, the first field being its name, the selects it needs
    /// a choice in, the first option taken, and the dates it needs, today taken. Each test gets its own copy of the
    /// arrays, as TUnit asks.
    /// </summary>
    public static IEnumerable<Func<(string Path, string Button, string[] Fields, string[] Selects, string[] Dates)>>
        Additions() =>
    [
        () => ("/Bills", "New bill", ["Name", "Paid to", "Amount"], ["Paid from"], ["First due"]),
        () => ("/Debts", "New debt", ["Name", "Owed", "Monthly payment"], [], []),
        () => ("/SavingsGoals", "New goal", ["Name", "Target"], [], []),
        () => ("/Classifications", "New tag", ["Tag"], [], []),
        () => ("/HouseholdMembers", "Add person", ["First name", "Last name"], [], []),
        () => ("/Calendar", "Add event", ["Title"], [], [])
    ];

    [Test]
    [MethodDataSource(nameof(Additions))]
    public async Task Add_FilledInAndSaved_IsListed(string path, string button, string[] fields, string[] selects,
        string[] dates)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);

        // Act
        await Button(page, button).ClickAsync();
        ILocator dialog = Dialog(page);
        string name = await FillInNewAsync(page, dialog, fields, selects, dates);
        bool fits = await FitsOnScreenAsync(page, dialog);
        List<string> problems = await AccessibilityProblemsAsync(page);
        await Button(page, "OK").ClickAsync();

        // Assert
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(page.GetByText(name).First).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(fits).IsTrue();
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    [MethodDataSource(nameof(Rows))]
    public async Task Edit_FromTheRowMenu_SavesTheChange(string path, string name, string editItem,
        string nameField)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);

        // Act
        await ChooseFromRowMenuAsync(page, name, editItem);
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        bool fits = await FitsOnScreenAsync(page, dialog);
        List<string> problems = await AccessibilityProblemsAsync(page);
        await dialog.GetByLabel(nameField, new LocatorGetByLabelOptions { Exact = true }).FillAsync(NewName);
        await Button(page, "OK").ClickAsync();

        // Assert, still there after loading the page again
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(page.GetByText(NewName).First).ToBeVisibleAsync();
        await GoToAsync(page, path);
        await Assertions.Expect(page.GetByText(NewName).First).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(fits).IsTrue();
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    public static IEnumerable<(string Path, string Name)> RowsToDelete() =>
        Rows().Select(static x => (x.Path, x.Name));

    [Test]
    [MethodDataSource(nameof(RowsToDelete))]
    public async Task Delete_FromTheRowMenuOnceConfirmed_RemovesIt(string path, string name)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);

        // Act
        await ChooseFromRowMenuAsync(page, name, "Delete");
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        bool fits = await FitsOnScreenAsync(page, dialog);
        List<string> problems = await AccessibilityProblemsAsync(page);
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete", Exact = true })
            .ClickAsync();

        // Assert, gone from the page and still gone after loading it again
        ILocator row = Button(page, $"Actions for {name}");
        await Assertions.Expect(row).ToHaveCountAsync(0);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        await Assertions.Expect(row).ToHaveCountAsync(0);
        using (Assert.Multiple())
        {
            await Assert.That(fits).IsTrue();
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    [MethodDataSource(nameof(Additions))]
    public async Task Add_ServerFailsToSave_SaysSoAndAddsNothing(string path, string button, string[] fields,
        string[] selects, string[] dates)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        await Button(page, button).ClickAsync();
        ILocator dialog = Dialog(page);
        string name = await FillInNewAsync(page, dialog, fields, selects, dates);
        await FailChangesAsync(page);

        // Act
        await Button(page, "OK").ClickAsync();

        // Assert
        await Assertions.Expect(FailureMessage(page)).ToBeVisibleAsync();
        await AssertUnchangedAsync(page, path, shown: null, notShown: name);
    }

    [Test]
    [MethodDataSource(nameof(Rows))]
    public async Task Edit_ServerFailsToSave_SaysSoAndChangesNothing(string path, string name, string editItem,
        string nameField)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        await ChooseFromRowMenuAsync(page, name, editItem);
        ILocator dialog = Dialog(page);
        await dialog.GetByLabel(nameField, new LocatorGetByLabelOptions { Exact = true }).FillAsync(NewName);
        await FailChangesAsync(page);

        // Act
        await Button(page, "OK").ClickAsync();

        // Assert
        await Assertions.Expect(FailureMessage(page)).ToBeVisibleAsync();
        await AssertUnchangedAsync(page, path, shown: name, notShown: NewName);
    }

    [Test]
    [MethodDataSource(nameof(RowsToDelete))]
    public async Task Delete_ServerFails_SaysSoAndKeepsIt(string path, string name)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        await ChooseFromRowMenuAsync(page, name, "Delete");
        ILocator dialog = Dialog(page);
        await FailChangesAsync(page);

        // Act
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete", Exact = true })
            .ClickAsync();

        // Assert
        await Assertions.Expect(FailureMessage(page)).ToBeVisibleAsync();
        await AssertUnchangedAsync(page, path, shown: name, notShown: null);
    }

    /// <summary>
    /// Fills in a new one's dialog: <paramref name="fields"/>, the first its name, the first option in each of
    /// <paramref name="selects"/> and today in each of <paramref name="dates"/>.
    /// </summary>
    /// <returns>The name it was given.</returns>
    private static async Task<string> FillInNewAsync(IPage page, ILocator dialog, string[] fields, string[] selects,
        string[] dates)
    {
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        string name = $"Added {Guid.NewGuid():N}"[..14];
        await dialog.GetByLabel(fields[0], new LocatorGetByLabelOptions { Exact = true }).FillAsync(name);
        foreach (string field in fields.Skip(1))
        {
            await dialog.GetByLabel(field, new LocatorGetByLabelOptions { Exact = true }).FillAsync(ValueFor(field));
        }

        foreach (string select in selects)
        {
            await ChooseFirstOptionAsync(page, dialog, select);
        }

        foreach (string date in dates)
        {
            await PickTodayAsync(page, dialog, date);
        }

        return name;
    }

    /// <summary>
    /// The server failing whatever changes something, while still answering what's asked for.
    /// </summary>
    private static async Task FailChangesAsync(IPage page) =>
        await page.RouteAsync("**/api/**", static route => AnswerAsync(route));

    private static Task AnswerAsync(IRoute route)
    {
        if (route.Request.Method == "GET")
        {
            return route.ContinueAsync();
        }

        return route.FulfillAsync(new RouteFulfillOptions { Status = 500 });
    }

    /// <summary>
    /// The page's message that what was asked for didn't happen, which each one ends by asking to try again.
    /// </summary>
    private static ILocator FailureMessage(IPage page) =>
        page.Locator(".mud-snackbar").Filter(new LocatorFilterOptions { HasText = "try again" });

    /// <summary>
    /// Once the server's back, the page as it was before: <paramref name="shown"/> still listed and
    /// <paramref name="notShown"/> not, and nothing gone wrong besides the failures asked for.
    /// </summary>
    private async Task AssertUnchangedAsync(IPage page, string path, string? shown, string? notShown)
    {
        await page.UnrouteAllAsync();
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        if (shown is not null)
        {
            await Assertions.Expect(Button(page, $"Actions for {shown}")).ToHaveCountAsync(1);
        }

        if (notShown is not null)
        {
            await Assertions.Expect(page.GetByText(notShown)).ToHaveCountAsync(0);
        }

        await Assert.That(BrowserErrors.Where(static x => !x.StartsWith("500 from", StringComparison.Ordinal)))
            .IsEmpty();
    }

    // Amounts for the money fields, a name for any other
    private static string ValueFor(string field) => field switch
    {
        "Amount" or "Owed" or "Monthly payment" or "Target" => "120",
        _ => "Example"
    };
}
