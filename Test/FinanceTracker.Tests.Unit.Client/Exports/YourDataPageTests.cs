using Microsoft.JSInterop;
using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using FinanceTracker.Tests.Unit.Client.PayPeriods;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using YourDataPage = FinanceTracker.Client.Pages.YourData;

namespace FinanceTracker.Tests.Unit.Client.Exports;

public class YourDataPageTests : ComponentTestBase
{
    private readonly IExportsService _exportsService = Substitute.For<IExportsService>();
    private readonly IPayPeriodsService _payPeriodsService = Substitute.For<IPayPeriodsService>();
    private readonly ApplicationState _applicationState = new();

    public YourDataPageTests()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("someone");
        Services.AddSingleton(_exportsService);
        Services.AddSingleton(_payPeriodsService);
        Services.AddScoped<IValidator<ExportBackup>, ExportBackupValidator>();
        // Paid on 30 October, so today, 4 November, is in November's period
        _payPeriodsService.GetYourPayAsync(Arg.Any<CancellationToken>()).Returns(YourPays.FromPayToPay());
        // The oldest transaction came in September's period
        _exportsService.GetStatementPeriodsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Period(2026, 10, 30, 2026, 11, 26, 11),
            Period(2026, 9, 30, 2026, 10, 29, 10),
            Period(2026, 8, 28, 2026, 9, 29, 9)
        ]);
        Services.AddSingleton<TimeProvider>(
            new FixedTimeProvider(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)));
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task Render_WarnsTheBackupIsNotEncrypted()
    {
        // Act
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Find(".mud-alert").TextContent).Contains("The backup isn't encrypted");
    }

    [Test]
    public async Task ProtectBackup_ShowsThePasswordFieldsAndTheRuleInsteadOfTheWarning()
    {
        // Arrange
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Act
        await ProtectAsync(cut);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll("label").Select(static x => x.TextContent.Trim()))
                .Contains("Password").And.Contains("Confirm password");
            await Assert.That(cut.Markup).Contains(BackupPasswordRules.Hint);
            await Assert.That(cut.Markup).Contains("can't be recovered");
            await Assert.That(cut.FindAll(".mud-alert")).IsEmpty();
        }
    }

    [Test]
    public async Task DownloadBackup_Protected_SendsThePasswordThenClearsTheFields()
    {
        // Arrange
        const string password = "Placeholder backup password";
        IRenderedComponent<YourDataPage> cut = RenderPage();
        await ProtectAsync(cut);
        await Field(cut, "Password").ChangeAsync(new() { Value = password });
        await Field(cut, "Confirm password").ChangeAsync(new() { Value = password });

        // Act
        await cut.InvokeAsync(() => DownloadButton(cut).Click());

        // Assert
        cut.WaitForState(() => JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile"));
        using (Assert.Multiple())
        {
            await _exportsService.Received(1).WriteBackupAsync(
                Arg.Is<ExportBackup>(static x => x.Password == password && x.ConfirmPassword == password),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            await Assert.That(Field(cut, "Password").GetAttribute("value") ?? string.Empty).IsEmpty();
            await Assert.That(Field(cut, "Confirm password").GetAttribute("value") ?? string.Empty).IsEmpty();
        }
    }

    [Test]
    public async Task DownloadBackup_ProtectedWithATooShortPassword_SaysWhyAndDownloadsNothing()
    {
        // Arrange
        string password = new('a', BackupPasswordRules.MinimumLength - 1);
        IRenderedComponent<YourDataPage> cut = RenderPage();
        await ProtectAsync(cut);
        await Field(cut, "Password").ChangeAsync(new() { Value = password });
        await Field(cut, "Confirm password").ChangeAsync(new() { Value = password });

        // Act
        await cut.InvokeAsync(() => DownloadButton(cut).Click());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains(BackupPasswordRules.TooShort, StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _exportsService.DidNotReceive().WriteBackupAsync(Arg.Any<ExportBackup>(), Arg.Any<Stream>(),
                Arg.Any<CancellationToken>());
            await Assert.That(JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile")).IsFalse();
        }
    }

    [Test]
    public async Task DownloadBackup_SavesItAsAZip()
    {
        // Arrange
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => DownloadButton(cut).Click());

        // Assert
        cut.WaitForState(() => JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile"));
        JSRuntimeInvocation saved = JSInterop.Invocations.Single(static x => x.Identifier == "ftDownloadFile");
        using (Assert.Multiple())
        {
            await _exportsService.Received(1).WriteBackupAsync(Arg.Is<ExportBackup>(static x => x.Password == null),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            await Assert.That(saved.Arguments[0]).IsEqualTo("fintrack-backup-2026-11-04.zip");
            await Assert.That(saved.Arguments[1]).IsEqualTo("application/zip");
        }
    }

    [Test]
    public async Task DownloadBackup_WhenItFails_SaysSoAndCanBeTriedAgain()
    {
        // Arrange
        _exportsService.WriteBackupAsync(Arg.Any<ExportBackup>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => DownloadButton(cut).Click());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Couldn't make your backup, try again"));
        using (Assert.Multiple())
        {
            await Assert.That(JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile")).IsFalse();
            await Assert.That(DownloadButton(cut).HasAttribute("disabled")).IsFalse();
        }
    }

    [Test]
    public async Task DownloadStatement_BrowserCantSaveIt_SaysSo()
    {
        // Arrange
        JSInterop.SetupVoid("ftDownloadFile", static _ => true).SetException(new JSException("SecurityError"));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => Button(cut, "Download statement").Click());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("make your statement", StringComparison.Ordinal));
    }

    [Test]
    public async Task DownloadStatement_IsTheLastWholePayPeriodToStartWith()
    {
        // Arrange
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => Button(cut, "Download statement").Click());

        // Assert
        cut.WaitForState(() => JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile"));
        JSRuntimeInvocation saved = JSInterop.Invocations.Single(static x => x.Identifier == "ftDownloadFile");
        using (Assert.Multiple())
        {
            await _exportsService.Received(1).WriteStatementAsync(2026, 10, Arg.Any<Stream>(),
                Arg.Any<CancellationToken>());
            await Assert.That(saved.Arguments[0]).IsEqualTo("fintrack-statement-2026-10.pdf");
            await Assert.That(saved.Arguments[1]).IsEqualTo("application/pdf");
        }
    }

    [Test]
    public async Task DownloadStatement_TheChosenPayPeriod()
    {
        // Arrange
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<YourDataPage> cut = RenderPage();
        await cut.Find(".ft-your-data__period").MouseDownAsync(new MouseEventArgs());
        IElement soFar = popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Single(static x => x.TextContent.Trim() == "November 2026, so far");
        await soFar.ClickAsync(new MouseEventArgs());

        // Act
        await cut.InvokeAsync(() => Button(cut, "Download statement").Click());

        // Assert
        cut.WaitForState(() => JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile"));
        await _exportsService.Received(1).WriteStatementAsync(2026, 11, Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StatementPeriods_AreOnlyThoseWithTransactions()
    {
        // Arrange
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Act
        await cut.Find(".ft-your-data__period").MouseDownAsync(new MouseEventArgs());

        // Assert
        IEnumerable<string> names = popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Select(static x => x.TextContent.Trim());
        await Assert.That(names).IsEquivalentTo(["November 2026, so far", "October 2026", "September 2026"]);
    }

    [Test]
    public async Task StatementPeriod_ShowsTheDaysItCovers()
    {
        // Arrange: the month's short name differs between ICU versions, e.g. Sep or Sept
        using CultureScope culture = new();
        string expected = $"{new DateOnly(2026, 9, 30).ToString("d MMM yyyy", CultureInfo.CurrentCulture)} to " +
                          $"{new DateOnly(2026, 10, 29).ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";

        // Act
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Find(".ft-your-data__period").TextContent).Contains(expected);
    }

    [Test]
    public async Task DownloadStatement_WhenItFails_SaysSo()
    {
        // Arrange
        _exportsService.WriteStatementAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<YourDataPage> cut = RenderPage();

        // Act
        await cut.InvokeAsync(() => Button(cut, "Download statement").Click());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Couldn't make your statement, try again"));
        await Assert.That(JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile")).IsFalse();
    }

    private static PayPeriod Period(int startYear, int startMonth, int startDay, int endYear, int endMonth,
        int endDay, int month) => new()
    {
        Start = new DateOnly(startYear, startMonth, startDay),
        End = new DateOnly(endYear, endMonth, endDay),
        Month = new DateOnly(2026, month, 1)
    };

    private IRenderedComponent<YourDataPage> RenderPage() =>
        Render<YourDataPage>(p => p.AddCascadingValue(_applicationState));

    private static Task ProtectAsync(IRenderedComponent<YourDataPage> cut) =>
        cut.Find(".mud-switch input").ChangeAsync(new() { Value = true });

    private static IElement Field(IRenderedComponent<YourDataPage> cut, string label)
    {
        string id = cut.FindAll("label").Single(x => x.TextContent.Trim() == label).GetAttribute("for")!;
        return cut.Find($"#{id}");
    }

    private static IElement DownloadButton(IRenderedComponent<YourDataPage> cut) =>
        Button(cut, "Download backup");

    private static IElement Button(IRenderedComponent<YourDataPage> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
