using Bunit;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using YourDataPage = FinanceTracker.Client.Pages.YourData;

namespace FinanceTracker.Tests.Unit.Client.Exports;

public class YourDataPageTests : ComponentTestBase
{
    private readonly IExportsService _exportsService = Substitute.For<IExportsService>();

    public YourDataPageTests()
    {
        AddAuthorization().SetAuthorized("someone");
        Services.AddSingleton(_exportsService);
        Services.AddSingleton<TimeProvider>(
            new FixedTimeProvider(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)));
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task Render_WarnsTheBackupIsNotEncrypted()
    {
        // Act
        IRenderedComponent<YourDataPage> cut = Render<YourDataPage>();

        // Assert
        await Assert.That(cut.Find(".mud-alert").TextContent).Contains("The backup isn't encrypted");
    }

    [Test]
    public async Task DownloadBackup_SavesItAsAZip()
    {
        // Arrange
        IRenderedComponent<YourDataPage> cut = Render<YourDataPage>();

        // Act
        await cut.InvokeAsync(() => DownloadButton(cut).Click());

        // Assert
        cut.WaitForState(() => JSInterop.Invocations.Any(static x => x.Identifier == "ftDownloadFile"));
        JSRuntimeInvocation saved = JSInterop.Invocations.Single(static x => x.Identifier == "ftDownloadFile");
        using (Assert.Multiple())
        {
            await _exportsService.Received(1).WriteBackupAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            await Assert.That(saved.Arguments[0]).IsEqualTo("fintrack-backup-2026-11-04.zip");
            await Assert.That(saved.Arguments[1]).IsEqualTo("application/zip");
        }
    }

    [Test]
    public async Task DownloadBackup_WhenItFails_SaysSoAndCanBeTriedAgain()
    {
        // Arrange
        _exportsService.WriteBackupAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The server didn't answer"));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<YourDataPage> cut = Render<YourDataPage>();

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

    private static AngleSharp.Dom.IElement DownloadButton(IRenderedComponent<YourDataPage> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Download backup");
}
