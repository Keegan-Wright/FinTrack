using Bunit;
using FinanceTracker.Client.Components.Notifications;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using static FinanceTracker.Tests.Unit.Client.Notifications.TestNotificationSettings;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

public class EmailSectionTests : ComponentTestBase
{
    private static readonly DateTime SentAt = new(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc);

    private readonly INotificationsService _service = Substitute.For<INotificationsService>();
    private readonly IProfileService _profile = Substitute.For<IProfileService>();

    public EmailSectionTests()
    {
        Services.AddSingleton(_service);
        Services.AddSingleton(_profile);
        Services.AddSingleton(TimeProvider.System);
    }

    [Test]
    public async Task Render_EmailSetUp_SaysWhereTheyGo()
    {
        // Act
        IRenderedComponent<EmailSection> cut = RenderSection(Create());

        // Assert
        await Assert.That(cut.Find(".ft-stack").TextContent).Contains($"Notifications are emailed to {Address}.");
    }

    [Test]
    public async Task Render_NoMailServer_SaysWhyWithoutATestButton()
    {
        // Act
        IRenderedComponent<EmailSection> cut = RenderSection(Create(emailAvailable: false));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Email isn't set up");
            await Assert.That(cut.FindAll("button")).IsEmpty();
        }
    }

    [Test]
    public async Task Render_NoAddress_SaysWhy()
    {
        // Act
        IRenderedComponent<EmailSection> cut = RenderSection(Create(emailAddress: null));

        // Assert
        await Assert.That(cut.Markup).Contains("no email address");
    }

    [Test]
    public async Task Render_NotConfirmed_AsksToConfirmInsteadOfATest()
    {
        // Act
        IRenderedComponent<EmailSection> cut = RenderSection(Create(emailConfirmed: false));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-stack").TextContent)
                .Contains($"Confirm {Address} is your email to be notified there.");
            await Assert.That(cut.FindAll("button").Select(static x => x.TextContent.Trim()))
                .IsEquivalentTo(["Send confirmation link"]);
        }
    }

    [Test]
    public async Task SendConfirmation_Sent_SaysWhereItWent()
    {
        // Arrange
        _profile.SendEmailConfirmationAsync(Arg.Any<CancellationToken>()).Returns(true);
        IRenderedComponent<EmailSection> cut = RenderSection(Create(emailConfirmed: false));

        // Act
        await cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Send confirmation link")
            .ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("on its way", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await Assert.That(cut.Find(".ft-stack").TextContent).Contains($"A link is on its way to {Address}.");
            await Assert.That(cut.FindAll("button")).IsEmpty();
        }
    }

    [Test]
    public async Task SendConfirmation_NothingToSend_ReloadsTheSettings()
    {
        // Arrange, confirmed in another tab meanwhile
        NotificationSettings confirmed = Create();
        _profile.SendEmailConfirmationAsync(Arg.Any<CancellationToken>()).Returns(false);
        _service.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(confirmed);
        IRenderedComponent<EmailSection> cut = RenderSection(Create(emailConfirmed: false));

        // Act
        await cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Send confirmation link")
            .ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Notifications are emailed to", StringComparison.Ordinal));
        await Assert.That(cut.Find(".ft-stack").TextContent).Contains($"Notifications are emailed to {Address}.");
    }

    [Test]
    public async Task TestEmail_Sent_SaysSoAndWhenBesideTheButton()
    {
        // Arrange
        _service.SendTestEmailAsync(Arg.Any<CancellationToken>())
            .Returns(Create(lastEmail: new DeliveryStatus { At = SentAt }));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<EmailSection> cut = RenderSection(Create());

        // Act
        await TestButton(cut).ClickAsync(new());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains($"Test email sent to {Address}", StringComparison.Ordinal));
        await Assert.That(cut.Find(".ft-email-test").TextContent).Contains("Last email sent");
    }

    [Test]
    public async Task TestEmail_Fails_SaysWhyBesideTheButton()
    {
        // Arrange
        _service.SendTestEmailAsync(Arg.Any<CancellationToken>()).Returns(Create(lastEmail: new DeliveryStatus
        {
            At = SentAt, Failure = "The mail server didn't accept FinTrack's sign in."
        }));
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<EmailSection> cut = RenderSection(Create());

        // Act
        await TestButton(cut).ClickAsync(new());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("Couldn't send the test email", StringComparison.Ordinal));
        await Assert.That(cut.Find(".ft-email-test .mud-error-text").TextContent)
            .Contains("The mail server didn't accept FinTrack's sign in.");
    }

    [Test]
    public async Task TestEmail_EmailNoLongerPossible_ReloadsTheSettingsToSayWhy()
    {
        // Arrange
        _service.SendTestEmailAsync(Arg.Any<CancellationToken>()).Returns((NotificationSettings?)null);
        _service.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Create(emailAvailable: false));
        IRenderedComponent<EmailSection> cut = RenderSection(Create());

        // Act
        await TestButton(cut).ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Email isn't set up", StringComparison.Ordinal));
    }

    private IRenderedComponent<EmailSection> RenderSection(NotificationSettings settings) =>
        Render<EmailSection>(p => p.Add(x => x.Settings, settings));

    private static AngleSharp.Dom.IElement TestButton(IRenderedComponent<EmailSection> cut) =>
        cut.FindAll("button").Single(static x => x.TextContent.Trim() == "Send a test email");
}
