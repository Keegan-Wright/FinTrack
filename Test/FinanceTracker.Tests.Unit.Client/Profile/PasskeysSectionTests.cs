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

public class PasskeysSectionTests : ComponentTestBase
{
    private const string Options = "{\"challenge\":\"placeholder\"}";
    private const string Credential = "{\"id\":\"placeholder\"}";

    private static readonly PasskeySummary Laptop = new()
    {
        Id = "abc", Name = "Placeholder laptop", AddedAt = new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero),
        Synced = true
    };

    private readonly IPasskeyService _passkeyService = Substitute.For<IPasskeyService>();

    public PasskeysSectionTests()
    {
        Services.AddSingleton(_passkeyService);
        Services.AddScoped<IValidator<AddPasskey>, AddPasskeyValidator>();
        Services.AddScoped<IValidator<RenamePasskey>, RenamePasskeyValidator>();
        Services.AddScoped<IValidator<ConfirmPassword>, ConfirmPasswordValidator>();
        JSInterop.Setup<bool>("ftPasskeysSupported").SetResult(true);
    }

    [Test]
    public async Task Render_WithAPasskey_ListsItWithWhenItWasAddedAndThatItSyncs()
    {
        // Arrange
        _passkeyService.GetPasskeysAsync(Arg.Any<CancellationToken>()).Returns([Laptop]);

        // Act
        IRenderedComponent<PasskeysSection> cut = RenderSection();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-passkeys__row").Count == 1);
        AngleSharp.Dom.IElement row = cut.Find(".ft-passkeys__row");
        using (Assert.Multiple())
        {
            await Assert.That(row.TextContent).Contains("Placeholder laptop");
            await Assert.That(row.TextContent).Contains("Synced to your other devices");
            await Assert.That(cut.Markup).Contains("Add a passkey");
        }
    }

    [Test]
    public async Task Render_BrowserWithoutPasskeys_SaysSoWithoutTheAddButton()
    {
        // Arrange
        _passkeyService.GetPasskeysAsync(Arg.Any<CancellationToken>()).Returns([]);
        JSInterop.Setup<bool>("ftPasskeysSupported").SetResult(false);

        // Act
        IRenderedComponent<PasskeysSection> cut = RenderSection();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("This browser can't make passkeys", StringComparison.Ordinal));
        await Assert.That(cut.FindAll("button").Select(static x => x.TextContent.Trim())).DoesNotContain("Add a passkey");
    }

    [Test]
    public async Task Add_WithANameAndPassword_HasTheBrowserMakeItThenAddsIt()
    {
        // Arrange
        _passkeyService.GetPasskeysAsync(Arg.Any<CancellationToken>()).Returns([], [Laptop]);
        _passkeyService.StartAddingAsync(Arg.Any<ConfirmPassword>(), Arg.Any<CancellationToken>())
            .Returns(new PasskeyChange { CreationOptionsJson = Options });
        _passkeyService.AddAsync(Arg.Any<AddPasskey>(), Arg.Any<CancellationToken>()).Returns(new PasskeyChange());
        JSInterop.Setup<string?>("ftCreatePasskey", Options).SetResult(Credential);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<PasskeysSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Add a passkey", StringComparison.Ordinal));

        // Act
        Task adding = Button(cut, "Add a passkey").ClickAsync(new());
        await FillAndConfirmAsync(dialogs, "Placeholder laptop", "my-password");
        await adding.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-passkeys__row").Count == 1);
        using (Assert.Multiple())
        {
            await _passkeyService.Received(1).StartAddingAsync(
                Arg.Is<ConfirmPassword>(x => x.Password == "my-password"), Arg.Any<CancellationToken>());
            await _passkeyService.Received(1).AddAsync(
                Arg.Is<AddPasskey>(x => x.CredentialJson == Credential && x.Name == "Placeholder laptop"),
                Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Add_CancelledInTheBrowser_AddsNothing()
    {
        // Arrange
        _passkeyService.GetPasskeysAsync(Arg.Any<CancellationToken>()).Returns([]);
        _passkeyService.StartAddingAsync(Arg.Any<ConfirmPassword>(), Arg.Any<CancellationToken>())
            .Returns(new PasskeyChange { CreationOptionsJson = Options });
        JSInterop.Setup<string?>("ftCreatePasskey", Options).SetResult(null);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<PasskeysSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Add a passkey", StringComparison.Ordinal));

        // Act
        Task adding = Button(cut, "Add a passkey").ClickAsync(new());
        await FillAndConfirmAsync(dialogs, "Placeholder laptop", "my-password");
        await adding.WaitAsync(TestTimeouts.Wait);

        // Assert
        await _passkeyService.DidNotReceive().AddAsync(Arg.Any<AddPasskey>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Add_WrongPassword_SaysWhyWithoutAskingTheBrowser()
    {
        // Arrange
        _passkeyService.GetPasskeysAsync(Arg.Any<CancellationToken>()).Returns([]);
        _passkeyService.StartAddingAsync(Arg.Any<ConfirmPassword>(), Arg.Any<CancellationToken>())
            .Returns(new PasskeyChange { Errors = ["Your password isn't right"] });
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<PasskeysSection> cut = RenderSection();
        cut.WaitForState(() => cut.Markup.Contains("Add a passkey", StringComparison.Ordinal));

        // Act
        Task adding = Button(cut, "Add a passkey").ClickAsync(new());
        await FillAndConfirmAsync(dialogs, "Placeholder laptop", "wrong");
        await adding.WaitAsync(TestTimeouts.Wait);

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Your password isn", StringComparison.Ordinal));
        await Assert.That(JSInterop.Invocations.Any(static x => x.Identifier == "ftCreatePasskey")).IsFalse();
    }

    [Test]
    public async Task Remove_WithThePassword_RemovesIt()
    {
        // Arrange
        _passkeyService.GetPasskeysAsync(Arg.Any<CancellationToken>())
            .Returns([Laptop], Array.Empty<PasskeySummary>());
        _passkeyService.RemoveAsync(Laptop.Id, Arg.Any<ConfirmPassword>(), Arg.Any<CancellationToken>())
            .Returns(new PasskeyChange());
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<PasskeysSection> cut = RenderSection();
        cut.WaitForState(() => cut.FindAll(".ft-passkeys__row").Count == 1);

        // Act
        Task removing = Button(cut, "Remove").ClickAsync(new());
        AngleSharp.Dom.IElement password = dialogs.WaitForElement(".mud-dialog input[type='password']");
        await password.ChangeAsync(new() { Value = "my-password" });
        await dialogs.FindAll(".mud-dialog-actions button").Single(static x => x.TextContent.Trim() == "OK")
            .ClickAsync(new());
        await removing.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-passkeys__row").Count == 0);
        await _passkeyService.Received(1).RemoveAsync(Laptop.Id,
            Arg.Is<ConfirmPassword>(x => x.Password == "my-password"), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Fills in the add dialog's name and password and presses OK.
    /// </summary>
    private static async Task FillAndConfirmAsync(IRenderedComponent<MudDialogProvider> dialogs, string name,
        string password)
    {
        AngleSharp.Dom.IElement nameField = dialogs.WaitForElement(".mud-dialog input:not([type='password'])");
        await nameField.InputAsync(new() { Value = name });
        await dialogs.Find(".mud-dialog input[type='password']").ChangeAsync(new() { Value = password });
        await dialogs.FindAll(".mud-dialog-actions button").Single(static x => x.TextContent.Trim() == "OK")
            .ClickAsync(new());
    }

    private IRenderedComponent<PasskeysSection> RenderSection()
    {
        Render<MudPopoverProvider>();
        return Render<PasskeysSection>(p => p.AddCascadingValue(new ApplicationState()));
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<PasskeysSection> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
