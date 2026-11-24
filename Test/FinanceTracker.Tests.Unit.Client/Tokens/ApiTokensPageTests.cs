using Bunit;
using Bunit.TestDoubles;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Tokens;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Tokens;

public class ApiTokensPageTests : ComponentTestBase
{
    private static readonly DateTime Made = new(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

    private static readonly TokenSummary HomeAssistant = new()
    {
        Id = Guid.NewGuid(), Name = "Home Assistant", CreatedAt = Made, ExpiresAt = null, LastUsedAt = Made,
        Expired = false
    };

    private static readonly TokenSummary OldScript = new()
    {
        Id = Guid.NewGuid(), Name = "Old script", CreatedAt = Made, ExpiresAt = Made.AddDays(30), LastUsedAt = null,
        Expired = true
    };

    private readonly ITokensService _tokensService = Substitute.For<ITokensService>();

    public ApiTokensPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_tokensService);
        Services.AddScoped<IValidator<CreateToken>, CreateTokenValidator>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Test]
    public async Task Render_WithTokens_MarksOnesThatNeverExpireOrHaveExpired()
    {
        // Arrange
        _tokensService.GetTokensAsync(Arg.Any<CancellationToken>()).Returns(Overview(HomeAssistant, OldScript));

        // Act
        IRenderedComponent<ApiTokensPage> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-tokens__row").Count == 2);
        using (Assert.Multiple())
        {
            await Assert.That(Row(cut, "Home Assistant").TextContent).Contains("Never expires");
            await Assert.That(Row(cut, "Home Assistant").TextContent).Contains("Last used");
            await Assert.That(Row(cut, "Old script").TextContent).Contains("Expired");
            await Assert.That(Row(cut, "Old script").TextContent).Contains("Never used");
        }
    }

    [Test]
    public async Task Render_CantHaveTokens_SaysTokensArentForThem()
    {
        // Arrange
        _tokensService.GetTokensAsync(Arg.Any<CancellationToken>())
            .Returns(new TokensOverview { Confirmation = null, Confirmed = false });

        // Act
        IRenderedComponent<ApiTokensPage> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Tokens are for accounts that sign in with a password or",
            StringComparison.Ordinal));
        await Assert.That(cut.FindAll("button").Select(static x => x.TextContent.Trim())).DoesNotContain("New token");
    }

    [Test]
    public async Task New_WithANameAndPassword_MakesItAndShowsItOnce()
    {
        // Arrange
        _tokensService.GetTokensAsync(Arg.Any<CancellationToken>()).Returns(Overview(), Overview(HomeAssistant));
        _tokensService.CreateAsync(Arg.Any<CreateToken>(), Arg.Any<CancellationToken>())
            .Returns(new TokenCreated("ftk_0123456789abcdef_placeholder"));
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<ApiTokensPage> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("New token", StringComparison.Ordinal));

        // Act
        Task making = Button(cut, "New token").ClickAsync(new());
        AngleSharp.Dom.IElement name = dialogs.WaitForElement(".mud-dialog input:not([type='password'])");
        await name.InputAsync(new() { Value = "Home Assistant" });
        await dialogs.Find(".mud-dialog input[type='password']").ChangeAsync(new() { Value = "my-password" });
        await dialogs.FindAll(".mud-dialog-actions button").Single(static x => x.TextContent.Trim() == "OK")
            .ClickAsync(new());
        await making.WaitAsync(TestTimeouts.Wait);

        // Assert
        dialogs.WaitForState(() => dialogs.Markup.Contains("ftk_0123456789abcdef_placeholder", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _tokensService.Received(1).CreateAsync(
                Arg.Is<CreateToken>(x => x.Name == "Home Assistant" && x.Password == "my-password" &&
                                         x.Expiry == TokenExpiry.NinetyDays),
                Arg.Any<CancellationToken>());
            await Assert.That(dialogs.Markup).Contains("You won't see it again");
        }
    }

    [Test]
    public async Task New_WrongPassword_SaysWhyWithoutShowingAToken()
    {
        // Arrange
        _tokensService.GetTokensAsync(Arg.Any<CancellationToken>()).Returns(Overview());
        _tokensService.CreateAsync(Arg.Any<CreateToken>(), Arg.Any<CancellationToken>())
            .Returns(new TokenCreationRefused(["Your password isn't right"]));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<ApiTokensPage> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("New token", StringComparison.Ordinal));

        // Act
        Task making = Button(cut, "New token").ClickAsync(new());
        AngleSharp.Dom.IElement name = dialogs.WaitForElement(".mud-dialog input:not([type='password'])");
        await name.InputAsync(new() { Value = "Home Assistant" });
        await dialogs.Find(".mud-dialog input[type='password']").ChangeAsync(new() { Value = "wrong" });
        await dialogs.FindAll(".mud-dialog-actions button").Single(static x => x.TextContent.Trim() == "OK")
            .ClickAsync(new());
        await making.WaitAsync(TestTimeouts.Wait);

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Your password isn", StringComparison.Ordinal));
        await Assert.That(dialogs.Markup).DoesNotContain("You won't see it again");
    }

    [Test]
    public async Task New_SingleSignOnUserNotYetConfirmed_GoesToConfirmAtTheProvider()
    {
        // Arrange
        _tokensService.GetTokensAsync(Arg.Any<CancellationToken>())
            .Returns(new TokensOverview { Confirmation = TokenConfirmation.SingleSignOn, Confirmed = false });
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<ApiTokensPage> cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("sign in there again", StringComparison.Ordinal));

        // Act
        await Button(cut, "New token").ClickAsync(new());

        // Assert
        await Assert.That(navigation.Uri).EndsWith("auth/oidc/confirm?returnUrl=%2FApiTokens%3Fnew%3Dtrue");
    }

    [Test]
    public async Task Back_FromConfirmingAtTheProvider_CarriesOnWithoutAskingForAPassword()
    {
        // Arrange
        _tokensService.GetTokensAsync(Arg.Any<CancellationToken>())
            .Returns(new TokensOverview { Confirmation = TokenConfirmation.SingleSignOn, Confirmed = true });
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("ApiTokens?new=true");
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();

        // Act
        RenderPage();

        // Assert, and the address is left as it is, as MudBlazor closes dialogs on the first navigation after a page
        // load, so clearing it would close this one in the browser
        dialogs.WaitForState(() => dialogs.FindAll(".mud-dialog").Count == 1);
        using (Assert.Multiple())
        {
            await Assert.That(dialogs.Markup).Contains("New API token");
            await Assert.That(dialogs.FindAll(".mud-dialog input[type='password']")).IsEmpty();
            await Assert.That(navigation.Uri).EndsWith("ApiTokens?new=true");
        }
    }

    [Test]
    public async Task Revoke_Confirmed_RevokesIt()
    {
        // Arrange
        _tokensService.GetTokensAsync(Arg.Any<CancellationToken>()).Returns(Overview(HomeAssistant), Overview());
        _tokensService.RevokeAsync(HomeAssistant.Id, Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<ApiTokensPage> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-tokens__row").Count == 1);

        // Act
        Task revoking = Button(cut, "Revoke").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Revoke");
        await revoking.WaitAsync(TestTimeouts.Wait);

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-tokens__row").Count == 0);
        await _tokensService.Received(1).RevokeAsync(HomeAssistant.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A password user's tokens.
    /// </summary>
    private static TokensOverview Overview(params TokenSummary[] tokens) =>
        new() { Confirmation = TokenConfirmation.Password, Confirmed = false, Tokens = tokens };

    private IRenderedComponent<ApiTokensPage> RenderPage() =>
        Render<ApiTokensPage>(p => p.AddCascadingValue(new ApplicationState()));

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<ApiTokensPage> cut, string name) =>
        cut.FindAll(".ft-tokens__row")
            .Single(x => x.QuerySelector(".ft-tokens__title")!.TextContent.Trim() == name);

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<ApiTokensPage> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
