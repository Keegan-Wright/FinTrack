using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// What a page does beyond adding, editing and deleting (<see cref="RowFlowTests"/>): ending and reopening a bill,
/// a goal's history, recognising someone's payments, a debt's own page, choosing your pay, editing a job's schedule
/// and drilling into a report.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class PageActionTests : UiTestBase
{
    [Test]
    public async Task Bill_EndedThenReopened_IsDueAgain()
    {
        // Arrange, the bill's due today
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Bills");
        await WaitUntilLoadedAsync(page);
        ILocator bill = page.Locator(".ft-bill").Filter(new LocatorFilterOptions { HasText = SeededUser.Bill });

        // Act, end it
        await ChooseFromRowMenuAsync(page, SeededUser.Bill, "End");
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "End", Exact = true })
            .ClickAsync();

        // Assert, listed once, in this month as it was due in it, as ended
        await Assertions.Expect(bill).ToHaveCountAsync(1);
        await Assertions.Expect(bill).ToContainTextAsync("Ended");
        await Assertions.Expect(page.GetByText("Nothing more is due on these")).ToHaveCountAsync(0);

        // Act, reopen it
        await ChooseFromRowMenuAsync(page, SeededUser.Bill, "Reopen");

        // Assert, running again, so it can be ended again
        await Assertions.Expect(bill).Not.ToContainTextAsync("Ended");
        await Button(page, $"Actions for {SeededUser.Bill}").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions
        {
            Name = "End", Exact = true
        })).ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    public static IEnumerable<(ScreenSize Screen, bool Dark)> BannerScreens() =>
    [
        (ScreenSize.Phone, false),
        (ScreenSize.Desktop, true)
    ];

    [Test]
    [MethodDataSource(nameof(BannerScreens))]
    public async Task RenewBanner_ConsentEndingSoon_IsAccessibleAndGoesToRenewIt(ScreenSize screen, bool dark)
    {
        // Arrange, the bank's consent ends in three days
        SignedInUser user = await RegisterSeededUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", NowFor(user).AddDays(3)));
        IPage page = await OpenAsync(screen, user, dark);
        await GoToAsync(page, "/");
        await WaitUntilLoadedAsync(page);
        ILocator banner = page.Locator(".ft-renew-banner");
        await Assertions.Expect(banner).ToContainTextAsync("A bank connection needs renewing to keep syncing.");
        List<string> problems = await AccessibilityProblemsAsync(page);
        List<string> tooWide = await WhatScrollsSidewaysAsync(page);

        // Act
        await banner.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Renew access" }).ClickAsync();

        // Assert
        await page.WaitForURLAsync("**/ProviderSetup");
        using (Assert.Multiple())
        {
            await Assert.That(problems).IsEmpty();
            await Assert.That(tooWide).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task SavingsGoalHistory_FromTheRowMenu_FitsAPhone()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/SavingsGoals");
        await WaitUntilLoadedAsync(page);

        // Act
        await ChooseFromRowMenuAsync(page, SeededUser.AccountGoal, "History");
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToContainTextAsync(SeededUser.AccountGoal);
        await WaitUntilLoadedAsync(page);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await FitsOnScreenAsync(page, dialog)).IsTrue();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task RecognisePayments_ANameAddedAndSaved_IsKept()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/HouseholdMembers");
        await WaitUntilLoadedAsync(page);
        const string payeeName = "S EXAMPLE";

        // Act
        await ChooseFromRowMenuAsync(page, $"{SeededUser.MemberFirstName} {SeededUser.MemberLastName}",
            "Recognise their payments");
        ILocator dialog = Dialog(page);
        await dialog.GetByLabel("Name", new LocatorGetByLabelOptions { Exact = true }).FillAsync(payeeName);
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add name", Exact = true })
            .ClickAsync();
        bool fits = await FitsOnScreenAsync(page, dialog);
        List<string> problems = await AccessibilityProblemsAsync(page);
        await Button(page, "OK").ClickAsync();

        // Assert, saved, and there when it's opened again
        await Assertions.Expect(page.GetByText($"Saved, none of your payments are {SeededUser.MemberFirstName}'s yet"))
            .ToBeVisibleAsync();
        await ChooseFromRowMenuAsync(page, $"{SeededUser.MemberFirstName} {SeededUser.MemberLastName}",
            "Recognise their payments");
        await Assertions.Expect(Dialog(page)).ToContainTextAsync(payeeName);
        using (Assert.Multiple())
        {
            await Assert.That(fits).IsTrue();
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task DebtPage_EditedThenDeleted_GoesBackToDebts()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, $"/Debts/{Scenario.DebtMappings[SeededUser.Debt]}");
        await WaitUntilLoadedAsync(page);
        const string renamed = "Car loan, renamed";

        // Act, rename it
        await ChooseFromRowMenuAsync(page, SeededUser.Debt, "Edit");
        await Dialog(page).GetByLabel("Name", new LocatorGetByLabelOptions { Exact = true }).FillAsync(renamed);
        await Button(page, "OK").ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText($"{renamed} saved")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".ft-page-header__title")).ToHaveTextAsync(renamed);

        // Act, delete it
        await ChooseFromRowMenuAsync(page, renamed, "Delete");
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete", Exact = true })
            .ClickAsync();

        // Assert
        await page.WaitForURLAsync("**/Debts");
        await Assertions.Expect(page.GetByText($"{renamed} deleted")).ToBeVisibleAsync();
        await Assertions.Expect(Button(page, $"Actions for {renamed}")).ToHaveCountAsync(0);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task Budgets_PaidMonthly_ChooseYourPayAndSwitchToCalendarMonths()
    {
        // Arrange, paid on the last working day of each of the last three months, which they follow
        SignedInUser user = await RegisterSeededUserAsync();
        DateOnly today = TodayFor(user);
        DateOnly[] payDays = [.. Enumerable.Range(1, 3)
            .Select(x => new DateOnly(today.Year, today.Month, 1).AddMonths(-x))
            .Select(static x => WorkingDays.LastIn(x.Year, x.Month))];
        foreach (DateOnly payDay in payDays)
        {
            await Scenario.Do(s => s.TransactionScenario.WithTransaction($"pay-{payDay}", SeededUser.Current,
                AcmePay(payDay)));
        }

        await Scenario.Do(s => s.PayScenario.WithPayOnTheLastWorkingDay(user.Username, SeededUser.Current, payDays));
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Budgets");
        await WaitUntilLoadedAsync(page);

        // Act, choose it again from the incomes
        await Button(page, "Choose another income").ClickAsync();
        ILocator dialog = Dialog(page);
        await ChooseFirstOptionAsync(page, dialog, "Your pay");
        bool fits = await FitsOnScreenAsync(page, dialog);
        List<string> problems = await AccessibilityProblemsAsync(page);
        await Button(page, "OK").ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();

        // Act, then switch to calendar months and back
        await Button(page, "Use calendar months").ClickAsync();
        await Button(page, "Use pay periods").ClickAsync();

        // Assert
        await Assertions.Expect(Button(page, "Use calendar months")).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(fits).IsTrue();
            await Assert.That(problems).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    /// <summary>
    /// Only opened and cancelled: saving changes a schedule every test shares, which the API tests cover.
    /// </summary>
    [Test]
    public async Task EditSchedule_OnAPhone_FitsAndIsAccessible()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, admin);
        await GoToAsync(page, "/Automation");

        // Act
        await ChooseFromRowMenuAsync(page, "Move event savings goals on", "Edit");
        ILocator dialog = Dialog(page);
        await Assertions.Expect(dialog).ToContainTextAsync("Edit job");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await FitsOnScreenAsync(page, dialog)).IsTrue();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }

        await Button(page, "Cancel").ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
    }

    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task Report_DrilledIntoDownToADay_ShowsItsTransactionsAndGoesBack(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/Reports/accounts");
        await WaitUntilLoadedAsync(page);

        // Act, the savings account, then a level deeper each time until there are only transactions
        await page.GetByRole(AriaRole.Button).Filter(new LocatorFilterOptions { HasText = SeededUser.Savings })
            .First.ClickAsync();
        // The drilldown's rows that go deeper, each with a chevron, which the accounts beside them don't have
        ILocator deeper = page.Locator(".ft-list__row--link[role=button]:has(.mud-icon-root)");
        for (int level = 0; level < 4 && await deeper.CountAsync() > 0; level++)
        {
            await deeper.First.ClickAsync();
            await WaitUntilLoadedAsync(page);
        }

        // Assert, the day's transaction, and the trail back up
        await Assertions.Expect(page.GetByText(SeededUser.FromCurrent).First).ToBeVisibleAsync();
        List<string> problems = await AccessibilityProblemsAsync(page);
        List<string> tooWide = await WhatScrollsSidewaysAsync(page);
        ILocator trail = page.Locator(".ft-trail__link");
        await trail.First.ClickAsync();
        await Assertions.Expect(trail).ToHaveCountAsync(0);
        using (Assert.Multiple())
        {
            await Assert.That(problems).IsEmpty();
            await Assert.That(tooWide).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    [Arguments("/Budgets")]
    [Arguments("/Bills")]
    public async Task MonthPicker_BackAMonthThenForward_ShowsEachMonth(string path)
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, path);
        await WaitUntilLoadedAsync(page);
        ILocator month = page.Locator(".ft-month-picker__label");
        string thisMonth = await month.InnerTextAsync();

        // Act, back
        await Button(page, "Previous month").ClickAsync();

        // Assert
        await Assertions.Expect(month).Not.ToHaveTextAsync(thisMonth);

        // Act, forward again
        await Button(page, "Next month").ClickAsync();

        // Assert
        await Assertions.Expect(month).ToHaveTextAsync(thisMonth);
        await Assert.That(BrowserErrors).IsEmpty();
    }

    [Test]
    public async Task Calendar_AnotherMonthAndDayChosen_ShowsThemAndTodayComesBack()
    {
        // Arrange
        SignedInUser user = await RegisterSeededUserAsync();
        IPage page = await OpenAsync(ScreenSize.Phone, user);
        await GoToAsync(page, "/Calendar");
        await WaitUntilLoadedAsync(page);
        ILocator month = page.Locator(".ft-page-header__text .mud-text-secondary");
        string thisMonth = await month.InnerTextAsync();

        // Act, last month's first day
        await Button(page, "Previous month").ClickAsync();
        await Assertions.Expect(month).Not.ToHaveTextAsync(thisMonth);
        await page.Locator(".ft-calendar__day").First.ClickAsync();

        // Assert, that day's shown instead of today
        await Assertions.Expect(page.GetByText(SeededUser.Event)).ToHaveCountAsync(0);

        // Act, back to today
        await Button(page, "Today").ClickAsync();

        // Assert
        await Assertions.Expect(month).ToHaveTextAsync(thisMonth);
        await Assertions.Expect(page.GetByText(SeededUser.Event).First).ToBeVisibleAsync();
        await Assert.That(BrowserErrors).IsEmpty();
    }

    private static TransactionDetails AcmePay(DateOnly day) =>
        new($"tx-{Guid.NewGuid():N}", "ACME LTD", TransactionType.Credit, "CREDIT", 2400m, "GBP",
            day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc), false, [], TransactionMetadata.None, false);
}
