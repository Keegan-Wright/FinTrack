using Bunit;
using FinanceTracker.Client.Components.Notifications;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

public class AlertsSectionTests : ComponentTestBase
{
    private static readonly Guid Everyday = Guid.NewGuid();
    private static readonly Guid Savings = Guid.NewGuid();

    private readonly INotificationsService _service = Substitute.For<INotificationsService>();
    private NotificationSettings? _changed;

    public AlertsSectionTests()
    {
        Services.AddSingleton(_service);
        Services.AddScoped<IValidator<ChangeAlertSettings>, ChangeAlertSettingsValidator>();
    }

    [Test]
    public async Task Render_Saved_ShowsThemWithSaveOffUntilSomethingChanges()
    {
        // Act
        IRenderedComponent<AlertsSection> cut = RenderSection(Settings(everydayBelow: 100m));

        // Assert, only the account with an alert is listed
        using (Assert.Multiple())
        {
            await Assert.That(Field(cut, "Notify at").GetAttribute("value")).IsEqualTo("80");
            await Assert.That(Field(cut, "Remind me").GetAttribute("value")).IsEqualTo("3");
            await Assert.That(cut.FindAll(".ft-alerts__account")).Count().IsEqualTo(1);
            await Assert.That(cut.Find(".ft-alerts__account").TextContent).Contains("Everyday");
            await Assert.That(cut.FindAll("label").Any(static x => x.TextContent.Trim() == "Payments of")).IsFalse();
            await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsTrue();
        }
    }

    [Test]
    public async Task Save_Changed_SavesThemAndHandsThePageTheResult()
    {
        // Arrange
        NotificationSettings saved = Settings(everydayBelow: 50m);
        _service.ChangeAlertsAsync(Arg.Any<ChangeAlertSettings>(), Arg.Any<CancellationToken>()).Returns(saved);
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<AlertsSection> cut = RenderSection(Settings(everydayBelow: 100m));

        // Act
        await Field(cut, "Notify at").InputAsync(new() { Value = "90" });
        await cut.Find(".ft-alerts__large input[type='checkbox']").ChangeAsync(new() { Value = true });
        await Field(cut, "Payments of").InputAsync(new() { Value = "250" });
        await Field(cut, "Below").InputAsync(new() { Value = "50" });
        await SaveButton(cut).ClickAsync(new());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Alerts saved", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _service.Received(1).ChangeAlertsAsync(
                Arg.Is<ChangeAlertSettings>(x => x.BudgetWarningPercent == 90 && x.BillReminderDays == 3
                                                 && x.LargeTransactionOver == 250m
                                                 && x.LowBalance.Single().AccountId == Everyday
                                                 && x.LowBalance.Single().Below == 50m),
                Arg.Any<CancellationToken>());
            await Assert.That(_changed).IsSameReferenceAs(saved);
        }
    }

    [Test]
    public async Task LargePayments_TurnedOnWithoutAnAmount_AsksForOneWithoutSaving()
    {
        // Arrange
        IRenderedComponent<AlertsSection> cut = RenderSection(Settings(everydayBelow: null));

        // Act
        await cut.Find(".ft-alerts__large input[type='checkbox']").ChangeAsync(new() { Value = true });
        await SaveButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Enter an amount", StringComparison.Ordinal));
        await _service.DidNotReceive().ChangeAlertsAsync(Arg.Any<ChangeAlertSettings>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddAnAlert_OnlyOffersAccountsWithoutOneAndListsTheChosenOne()
    {
        // Arrange
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<AlertsSection> cut = RenderSection(Settings(everydayBelow: 100m));

        // Act
        await cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Add an alert").ClickAsync(new());
        popovers.WaitForState(() => popovers.FindAll(".mud-menu-item").Count > 0);
        List<string> offered = popovers.FindAll(".mud-menu-item").Select(static x => x.TextContent.Trim()).ToList();
        await popovers.FindAll(".mud-menu-item").Single().ClickAsync(new());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(offered).IsEquivalentTo(["Savings"]);
            await Assert.That(cut.FindAll(".ft-alerts__account")).Count().IsEqualTo(2);
            await Assert.That(cut.FindAll("button").Any(static x => x.TextContent.Trim() == "Add an alert")).IsFalse();
            await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsFalse();
        }
    }

    [Test]
    public async Task RemoveAnAlert_TakesItOffTheList()
    {
        // Arrange
        IRenderedComponent<AlertsSection> cut = RenderSection(Settings(everydayBelow: 100m));

        // Act
        await cut.Find("[aria-label='Remove the alert for Everyday']").ClickAsync(new());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-alerts__account")).IsEmpty();
            await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsFalse();
        }
    }

    [Test]
    public async Task Render_NoAccounts_SaysToConnectABank()
    {
        // Act
        IRenderedComponent<AlertsSection> cut = RenderSection(Settings(everydayBelow: null, withAccounts: false));

        // Assert
        await Assert.That(cut.Markup).Contains("Connect a bank to add alerts for its accounts.");
    }

    private IRenderedComponent<AlertsSection> RenderSection(NotificationSettings settings) =>
        Render<AlertsSection>(p => p
            .Add(x => x.Settings, settings)
            .Add(x => x.SettingsChanged, changed => _changed = changed));

    private static AngleSharp.Dom.IElement SaveButton(IRenderedComponent<AlertsSection> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Save alerts");

    /// <summary>
    /// The field labelled <paramref name="label"/>, found by its label as a screen reader reads it.
    /// </summary>
    private static AngleSharp.Dom.IElement Field(IRenderedComponent<AlertsSection> cut, string label)
    {
        string id = cut.FindAll("label").Single(x => x.TextContent.Trim() == label).GetAttribute("for")!;
        return cut.Find($"#{id}");
    }

    /// <summary>
    /// The defaults, with an Everyday and a Savings account unless <paramref name="withAccounts"/> is false.
    /// </summary>
    private static NotificationSettings Settings(decimal? everydayBelow, bool withAccounts = true)
    {
        List<AccountLowBalance> accounts = [];
        if (withAccounts)
        {
            accounts.Add(new AccountLowBalance { AccountId = Everyday, AccountName = "Everyday", Below = everydayBelow });
            accounts.Add(new AccountLowBalance { AccountId = Savings, AccountName = "Savings", Below = null });
        }

        return new NotificationSettings
        {
            InApp = Enum.GetValues<NotificationKind>(),
            Emailed = Enum.GetValues<NotificationKind>(),
            Webhooked = Enum.GetValues<NotificationKind>(),
            EmailAvailable = false,
            BudgetWarningPercent = 80,
            BillReminderDays = 3,
            LowBalance = accounts
        };
    }
}
