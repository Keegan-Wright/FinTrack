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

public class WebhookSectionTests : ComponentTestBase
{
    private const string Url = "https://hooks.example.test/fintrack";

    private static readonly NotificationSettings Without = Settings(null);
    private static readonly NotificationSettings With = Settings(Url);

    private readonly INotificationsService _service = Substitute.For<INotificationsService>();
    private NotificationSettings? _changed;

    public WebhookSectionTests()
    {
        Services.AddSingleton(_service);
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<IValidator<SetWebhook>, SetWebhookValidator>();
    }

    [Test]
    public async Task Save_WholeAddress_SetsItAndShowsTheSecretHidden()
    {
        // Arrange
        _service.SetWebhookAsync(Arg.Any<SetWebhook>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookChange { Settings = With });
        IRenderedComponent<WebhookSection> cut = RenderSection(Without);

        // Act
        await cut.Find("input[type='url']").InputAsync(new() { Value = Url });
        await Button(cut, "Save").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Signing secret", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await _service.Received(1).SetWebhookAsync(Arg.Is<SetWebhook>(x => x.Url == Url),
                Arg.Any<CancellationToken>());
            await Assert.That(_changed).IsSameReferenceAs(With);
            await Assert.That(cut.Find("input[type='password']").GetAttribute("value")).IsEqualTo("secret");
            await Assert.That(Button(cut, "Change").HasAttribute("disabled")).IsTrue();
        }
    }

    [Test]
    public async Task Save_NotAnAddress_SaysSoWithoutSaving()
    {
        // Arrange
        IRenderedComponent<WebhookSection> cut = RenderSection(Without);

        // Act
        await cut.Find("input[type='url']").InputAsync(new() { Value = "hooks.example.test" });
        await Button(cut, "Save").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("starting https://", StringComparison.Ordinal));
        await _service.DidNotReceive().SetWebhookAsync(Arg.Any<SetWebhook>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Save_Refused_SaysWhyOnTheField()
    {
        // Arrange
        _service.SetWebhookAsync(Arg.Any<SetWebhook>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookChange { Refusal = "This FinTrack doesn't post to addresses on a private network." });
        IRenderedComponent<WebhookSection> cut = RenderSection(Without);

        // Act
        await cut.Find("input[type='url']").InputAsync(new() { Value = "http://192.168.1.10/hook" });
        await Button(cut, "Save").ClickAsync(new());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("private network", StringComparison.Ordinal));
        await Assert.That(_changed).IsNull();
    }

    [Test]
    public async Task Secret_ShowClicked_ShowsIt()
    {
        // Arrange
        IRenderedComponent<WebhookSection> cut = RenderSection(With);

        // Act
        await cut.Find("[aria-label='Show the secret']").ClickAsync(new());

        // Assert
        await Assert.That(cut.FindAll("input[type='password']")).IsEmpty();
    }

    [Test]
    public async Task Remove_Confirmed_RemovesIt()
    {
        // Arrange
        _service.RemoveWebhookAsync(Arg.Any<CancellationToken>()).Returns(Without);
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<WebhookSection> cut = RenderSection(With);

        // Act
        Task removing = Button(cut, "Remove").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Remove");
        await removing;

        // Assert
        await _service.Received(1).RemoveWebhookAsync(Arg.Any<CancellationToken>());
        cut.WaitForState(() => !cut.Markup.Contains("Signing secret", StringComparison.Ordinal));
    }

    [Test]
    public async Task NewSecret_Confirmed_ReplacesIt()
    {
        // Arrange
        _service.ReplaceWebhookSecretAsync(Arg.Any<CancellationToken>()).Returns(Settings(Url, "secret-two"));
        IRenderedComponent<MudDialogProvider> dialogs = Render<MudDialogProvider>();
        IRenderedComponent<WebhookSection> cut = RenderSection(With);

        // Act
        Task replacing = Button(cut, "New secret").ClickAsync(new());
        await RowMenu.ConfirmAsync(dialogs, "Make a new secret");
        await replacing;

        // Assert, shown so it can be copied to whatever receives the posts
        cut.WaitForState(() => cut.FindAll("input[type='password']").Count == 0);
        await Assert.That(_changed!.WebhookSecret).IsEqualTo("secret-two");
    }

    [Test]
    public async Task TestPost_Failed_SaysWhy()
    {
        // Arrange
        NotificationSettings failed = Settings(Url);
        failed = new NotificationSettings
        {
            Emailed = failed.Emailed, Webhooked = failed.Webhooked, EmailAvailable = false,
            WebhookUrl = Url, WebhookSecret = "secret",
            LastWebhook = new DeliveryStatus { At = DateTime.UtcNow, Failure = "The webhook answered 404, not success." }
        };
        _service.SendTestWebhookAsync(Arg.Any<CancellationToken>()).Returns(failed);
        IRenderedComponent<MudSnackbarProvider> snackbars = Render<MudSnackbarProvider>();
        IRenderedComponent<WebhookSection> cut = RenderSection(With);

        // Act
        await Button(cut, "Send a test post").ClickAsync(new());

        // Assert
        snackbars.WaitForState(() => snackbars.Markup.Contains("The test post failed", StringComparison.Ordinal));
        await Assert.That(cut.Find(".mud-error-text").TextContent).Contains("The webhook answered 404, not success.");
    }

    private IRenderedComponent<WebhookSection> RenderSection(NotificationSettings settings) =>
        Render<WebhookSection>(p => p
            .Add(x => x.Settings, settings)
            .Add(x => x.SettingsChanged, changed => _changed = changed));

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<WebhookSection> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);

    private static NotificationSettings Settings(string? url, string secret = "secret") => new()
    {
        Emailed = Enum.GetValues<NotificationKind>(), Webhooked = Enum.GetValues<NotificationKind>(),
        EmailAvailable = false, WebhookUrl = url, WebhookSecret = url is null ? null : secret
    };
}
