using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Components.Profile;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Profile;

public class TwoFactorSectionTests : ComponentTestBase
{
    private static readonly TwoFactorStatus Off =
        new() { Enabled = false, RecoveryCodesLeft = 0, BrowserRemembered = false };

    private readonly IProfileService _profileService = Substitute.For<IProfileService>();

    public TwoFactorSectionTests()
    {
        Services.AddSingleton(_profileService);
        Services.AddScoped<IValidator<EnableAuthenticator>, EnableAuthenticatorValidator>();
        Services.AddScoped<IValidator<ConfirmPassword>, ConfirmPasswordValidator>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Test]
    public async Task SetUp_WithACodeAndPassword_TurnsItOnAndShowsTheRecoveryCodesOnce()
    {
        // Arrange
        _profileService.GetTwoFactorAsync(Arg.Any<CancellationToken>()).Returns(Off,
            new TwoFactorStatus { Enabled = true, RecoveryCodesLeft = 10, BrowserRemembered = false });
        _profileService.GetAuthenticatorSetupAsync(Arg.Any<CancellationToken>())
            .Returns(new AuthenticatorSetup { SharedKey = "abcd efgh ijkl mnop", QrCodeSvg = "<svg></svg>" });
        _profileService.EnableAuthenticatorAsync(Arg.Any<EnableAuthenticator>(), Arg.Any<CancellationToken>())
            .Returns(new RecoveryCodesMade(["aaaa-1111", "bbbb-2222"]));
        IRenderedComponent<TwoFactorSection> cut = RenderSection();

        // Act
        await Button(cut, "Set up two-factor sign in").ClickAsync(new());
        cut.WaitForState(() => cut.Markup.Contains("abcd efgh ijkl mnop", StringComparison.Ordinal));
        await Field(cut, "Code").ChangeAsync(new() { Value = "123 456" });
        await Field(cut, "Your password").ChangeAsync(new() { Value = "my-password" });
        await Button(cut, "Turn on").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-two-factor__codes li").Count == 2);
        await _profileService.Received(1).EnableAuthenticatorAsync(
            Arg.Is<EnableAuthenticator>(x => x.Code == "123 456" && x.Password == "my-password"),
            Arg.Any<CancellationToken>());

        // And once saved, they're gone
        await Button(cut, "I've saved them").ClickAsync(new());
        cut.WaitForState(() => cut.Markup.Contains("10 recovery codes left", StringComparison.Ordinal));
        await Assert.That(cut.FindAll(".ft-two-factor__codes")).IsEmpty();
    }

    [Test]
    public async Task SetUp_WrongCode_SaysWhy()
    {
        // Arrange
        _profileService.GetTwoFactorAsync(Arg.Any<CancellationToken>()).Returns(Off);
        _profileService.GetAuthenticatorSetupAsync(Arg.Any<CancellationToken>())
            .Returns(new AuthenticatorSetup { SharedKey = "abcd", QrCodeSvg = "<svg></svg>" });
        _profileService.EnableAuthenticatorAsync(Arg.Any<EnableAuthenticator>(), Arg.Any<CancellationToken>())
            .Returns(new TwoFactorChangeRefused(["That code isn't right, try the one your app shows now"]));
        IRenderedComponent<TwoFactorSection> cut = RenderSection();

        // Act
        await Button(cut, "Set up two-factor sign in").ClickAsync(new());
        cut.WaitForState(() => cut.Markup.Contains("Turn on", StringComparison.Ordinal));
        await Field(cut, "Code").ChangeAsync(new() { Value = "000000" });
        await Field(cut, "Your password").ChangeAsync(new() { Value = "my-password" });
        await Button(cut, "Turn on").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.FindAll("[role='alert']").Count == 1);
        await Assert.That(cut.Find("[role='alert']").TextContent).Contains("That code isn't right");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_On_ShowsTheCodesLeftAndForgetsThisBrowserOnlyWhenRemembered(bool remembered)
    {
        // Arrange
        _profileService.GetTwoFactorAsync(Arg.Any<CancellationToken>())
            .Returns(new TwoFactorStatus { Enabled = true, RecoveryCodesLeft = 1, BrowserRemembered = remembered });

        // Act
        IRenderedComponent<TwoFactorSection> cut = RenderSection();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("1 recovery code left", StringComparison.Ordinal));
        string[] buttons = cut.FindAll("button").Select(static x => x.TextContent.Trim()).ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(buttons).Contains("Make new recovery codes");
            await Assert.That(buttons).Contains("Reset authenticator");
            await Assert.That(buttons).Contains("Turn off");
            await Assert.That(buttons.Contains("Forget this browser")).IsEqualTo(remembered);
        }
    }

    [Test]
    public async Task TurnOff_AsksForThePassword()
    {
        // Arrange
        _profileService.GetTwoFactorAsync(Arg.Any<CancellationToken>())
            .Returns(new TwoFactorStatus { Enabled = true, RecoveryCodesLeft = 10, BrowserRemembered = false },
                Off);
        _profileService.TurnOffTwoFactorAsync(Arg.Any<ConfirmPassword>(), Arg.Any<CancellationToken>())
            .Returns(new TwoFactorChangeMade());
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<TwoFactorSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Turn off", StringComparison.Ordinal));

        // Act
        Task turningOff = Button(cut, "Turn off").ClickAsync(new());
        AngleSharp.Dom.IElement password = dialogs.WaitForElement(".mud-dialog input[type='password']");
        await password.ChangeAsync(new() { Value = "my-password" });
        await dialogs.FindAll(".mud-dialog-actions button").Single(static x => x.TextContent.Trim() == "OK")
            .ClickAsync(new());
        await turningOff.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Set up two-factor sign in", StringComparison.Ordinal));
        await _profileService.Received(1).TurnOffTwoFactorAsync(
            Arg.Is<ConfirmPassword>(x => x.Password == "my-password"), Arg.Any<CancellationToken>());
    }

    private IRenderedComponent<TwoFactorSection> RenderSection()
    {
        Render<MudPopoverProvider>();
        Render<MudSnackbarProvider>();
        return Render<TwoFactorSection>(p => p.AddCascadingValue(new ApplicationState()));
    }

    /// <summary>
    /// The field labelled <paramref name="label"/>, found by its label as a screen reader reads it.
    /// </summary>
    private static AngleSharp.Dom.IElement Field(IRenderedComponent<TwoFactorSection> cut, string label)
    {
        string id = cut.FindAll("label").Single(x => x.TextContent.Trim() == label).GetAttribute("for")!;
        return cut.Find($"#{id}");
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<TwoFactorSection> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
