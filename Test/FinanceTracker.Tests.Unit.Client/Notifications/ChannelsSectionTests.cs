using Bunit;
using FinanceTracker.Client.Components.Notifications;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static FinanceTracker.Tests.Unit.Client.Notifications.TestNotificationSettings;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

public class ChannelsSectionTests : ComponentTestBase
{
    private readonly INotificationsService _service = Substitute.For<INotificationsService>();
    private NotificationSettings? _changed;

    public ChannelsSectionTests() => Services.AddSingleton(_service);

    [Test]
    public async Task Render_EmailOnly_HasInAppAndEmailColumnsAndNoWebhookOne()
    {
        // Act
        IRenderedComponent<ChannelsSection> cut = RenderSection(Create());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-channels thead th").Select(static x => x.TextContent.Trim()))
                .IsEquivalentTo(["Notification", "In app", "Email"],
                    TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(BoxCount(cut, "In app:")).IsEqualTo(Kinds);
            await Assert.That(BoxCount(cut, "Webhook:")).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Render_NeitherEmailNorWebhook_StillChoosesWhatShowsInTheApp()
    {
        // Act
        IRenderedComponent<ChannelsSection> cut = RenderSection(Create(emailAddress: null, emailAvailable: false));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-channels thead th").Select(static x => x.TextContent.Trim()))
                .IsEquivalentTo(["Notification", "In app"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(cut.Markup).Contains("Set up email or a webhook");
        }
    }

    [Test]
    public async Task Change_OneEmailTurnedOff_EmailsTheRestAndKeepsWhatsElsewhere()
    {
        // Arrange
        GivenChangeAnswered();
        IRenderedComponent<ChannelsSection> cut = RenderSection(Create());

        // Act
        await Box(cut, "Email: A bank failing to sync").ChangeAsync(new() { Value = false });

        // Assert
        using (Assert.Multiple())
        {
            await _service.Received(1).ChangeSettingsAsync(
                Arg.Is<ChangeNotificationSettings>(x => !x.Emailed.Contains(NotificationKind.SyncFailed)
                                                        && x.Emailed.Count == Kinds - 1
                                                        && x.Webhooked.Count == Kinds && x.InApp.Count == Kinds),
                Arg.Any<CancellationToken>());
            await Assert.That(_changed!.Emailed).DoesNotContain(NotificationKind.SyncFailed);
        }
    }

    [Test]
    public async Task Change_PostedOneTurnedOff_KeepsWhatsEmailed()
    {
        // Arrange
        GivenChangeAnswered(Hook);
        IRenderedComponent<ChannelsSection> cut = RenderSection(Create(webhookUrl: Hook));

        // Act
        await Box(cut, "Webhook: A bank's access having ended").ChangeAsync(new() { Value = false });

        // Assert
        await _service.Received(1).ChangeSettingsAsync(
            Arg.Is<ChangeNotificationSettings>(x => !x.Webhooked.Contains(NotificationKind.ConsentEnded)
                                                    && x.Webhooked.Count == Kinds - 1 && x.Emailed.Count == Kinds),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Change_InAppTurnedOff_KeepsWhatsEmailedAndPosted()
    {
        // Arrange
        GivenChangeAnswered(Hook);
        IRenderedComponent<ChannelsSection> cut = RenderSection(Create(webhookUrl: Hook));

        // Act
        await Box(cut, "In app: A bill coming up").ChangeAsync(new() { Value = false });

        // Assert
        await _service.Received(1).ChangeSettingsAsync(
            Arg.Is<ChangeNotificationSettings>(x => !x.InApp.Contains(NotificationKind.BillDueSoon)
                                                    && x.InApp.Count == Kinds - 1 && x.Emailed.Count == Kinds
                                                    && x.Webhooked.Count == Kinds),
            Arg.Any<CancellationToken>());
    }

    private IRenderedComponent<ChannelsSection> RenderSection(NotificationSettings settings) =>
        Render<ChannelsSection>(p => p
            .Add(x => x.Settings, settings)
            .Add(x => x.SettingsChanged, changed => _changed = changed));

    /// <summary>
    /// Changing the grid answers with what was asked for, as the server does.
    /// </summary>
    private void GivenChangeAnswered(string? webhookUrl = null) =>
        _service.ChangeSettingsAsync(Arg.Any<ChangeNotificationSettings>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                ChangeNotificationSettings change = call.Arg<ChangeNotificationSettings>();
                return Create(webhookUrl: webhookUrl, inApp: change.InApp, emailed: change.Emailed,
                    webhooked: change.Webhooked);
            });

    /// <summary>
    /// The checkbox named <paramref name="label"/>, found by its name as a screen reader reads it: MudBlazor labels
    /// it by a hidden span.
    /// </summary>
    private static AngleSharp.Dom.IElement Box(IRenderedComponent<ChannelsSection> cut, string label)
    {
        string id = cut.FindAll(".mud-sr-only").Single(x => x.TextContent == label).Id!;
        return cut.Find($"input[aria-labelledby='{id}']");
    }

    private static int BoxCount(IRenderedComponent<ChannelsSection> cut, string prefix) =>
        cut.FindAll(".mud-sr-only").Count(x => x.TextContent.StartsWith(prefix, StringComparison.Ordinal));
}
