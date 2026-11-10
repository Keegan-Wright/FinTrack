using Bunit;
using FinanceTracker.Client.Components.Profile;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using static FinanceTracker.Tests.Unit.Client.Profile.TestProfiles;

namespace FinanceTracker.Tests.Unit.Client.Profile;

public class ChangeEmailSectionTests : ComponentTestBase
{
    private readonly IProfileService _service = Substitute.For<IProfileService>();
    private int _changed;

    public ChangeEmailSectionTests()
    {
        Services.AddSingleton(_service);
        Services.AddScoped<IValidator<ChangeEmail>, ChangeEmailValidator>();
    }

    [Test]
    public async Task Render_Confirmed_ShowsTheEmailMarkedConfirmed()
    {
        // Act
        IRenderedComponent<ChangeEmailSection> cut = RenderSection(Create());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-profile__email").TextContent).Contains(Address);
            await Assert.That(cut.Find(".ft-profile__email .mud-chip").TextContent.Trim()).IsEqualTo("Confirmed");
            await Assert.That(cut.Markup).DoesNotContain("until you confirm");
        }
    }

    [Test]
    public async Task Render_NotConfirmed_SaysWhatThatMeans()
    {
        // Act
        IRenderedComponent<ChangeEmailSection> cut = RenderSection(Create(emailConfirmed: false));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-profile__email .mud-chip").TextContent.Trim()).IsEqualTo("Not confirmed");
            await Assert.That(cut.Markup)
                .Contains("Notifications and password reset links aren't emailed to it until you confirm it's yours.");
        }
    }

    [Test]
    [Arguments(true, "We'll email a link to the new one.")]
    [Arguments(false, "Your account uses it straight away.")]
    public async Task Render_SaysHowTheChangeIsMade(bool mailAvailable, string how)
    {
        // Act
        IRenderedComponent<ChangeEmailSection> cut = RenderSection(Create(mailAvailable: mailAvailable));

        // Assert
        await Assert.That(cut.Find(".mud-input-helper-text").TextContent).Contains(how);
    }

    [Test]
    public async Task Change_WithAMailServer_SaysWhereTheLinkWent()
    {
        // Arrange
        _service.ChangeEmailAsync(Arg.Any<ChangeEmail>(), Arg.Any<CancellationToken>())
            .Returns(new ProfileChange { LinkSent = true });
        IRenderedComponent<ChangeEmailSection> cut = RenderSection(Create());

        // Act
        await cut.Find("input").InputAsync(new() { Value = "new@example.test" });
        await ChangeButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("on its way", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _service.Received(1).ChangeEmailAsync(Arg.Is<ChangeEmail>(x => x.NewEmail == "new@example.test"),
                Arg.Any<CancellationToken>());
            await Assert.That(cut.Find("[role='status']").TextContent)
                .Contains("A link is on its way to new@example.test.");
            await Assert.That(_changed).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Change_WithoutAMailServer_TellsThePageItChanged()
    {
        // Arrange
        _service.ChangeEmailAsync(Arg.Any<ChangeEmail>(), Arg.Any<CancellationToken>()).Returns(new ProfileChange());
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<ChangeEmailSection> cut = RenderSection(Create(mailAvailable: false));

        // Act
        await cut.Find("input").InputAsync(new() { Value = "new@example.test" });
        await ChangeButton(cut).ClickAsync(new());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Email changed", StringComparison.Ordinal));
        await Assert.That(_changed).IsEqualTo(1);
    }

    [Test]
    public async Task Change_Refused_SaysWhyOnTheField()
    {
        // Arrange
        _service.ChangeEmailAsync(Arg.Any<ChangeEmail>(), Arg.Any<CancellationToken>())
            .Returns(new ProfileChange { Errors = ["Another account uses that email"] });
        IRenderedComponent<ChangeEmailSection> cut = RenderSection(Create());

        // Act
        await cut.Find("input").InputAsync(new() { Value = "taken@example.test" });
        await ChangeButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Another account", StringComparison.Ordinal));
        await Assert.That(cut.Find(".mud-input-helper-text").TextContent)
            .Contains("Another account uses that email");
    }

    [Test]
    public async Task Change_NotAnEmail_AsksForOneWithoutChanging()
    {
        // Arrange
        IRenderedComponent<ChangeEmailSection> cut = RenderSection(Create());

        // Act
        await cut.Find("input").InputAsync(new() { Value = "not an email" });
        await ChangeButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Enter an email address", StringComparison.Ordinal));
        await _service.DidNotReceive().ChangeEmailAsync(Arg.Any<ChangeEmail>(), Arg.Any<CancellationToken>());
    }

    private IRenderedComponent<ChangeEmailSection> RenderSection(ProfileDetails profile) =>
        Render<ChangeEmailSection>(p => p
            .Add(x => x.Profile, profile)
            .Add(x => x.ProfileChanged, () => _changed++));

    private static AngleSharp.Dom.IElement ChangeButton(IRenderedComponent<ChangeEmailSection> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Change email");
}
