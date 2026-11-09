using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static FinanceTracker.Tests.Unit.Client.Notifications.TestNotificationSettings;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

public class NotificationSettingsPageTests : ComponentTestBase
{
    private readonly INotificationsService _service = Substitute.For<INotificationsService>();

    public NotificationSettingsPageTests()
    {
        AddBunitPersistentComponentState();
        Services.AddSingleton(_service);
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<IValidator<SetWebhook>, SetWebhookValidator>();
        Services.AddScoped<IValidator<ChangeAlertSettings>, ChangeAlertSettingsValidator>();
    }

    [Test]
    public async Task Render_Loaded_HasATabEachOpenOnWhatAndWhere()
    {
        // Arrange
        _service.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(Create());

        // Act
        IRenderedComponent<NotificationSettingsPage> cut = RenderPage();

        // Assert
        cut.WaitForState(() => cut.FindAll(".ft-channels").Count == 1);
        await Assert.That(cut.FindAll(".mud-tab").Select(static x => x.TextContent.Trim()))
            .IsEquivalentTo(["What and where", "Alerts", "Email", "Webhook"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private IRenderedComponent<NotificationSettingsPage> RenderPage() =>
        Render<NotificationSettingsPage>(p => p.AddCascadingValue(new ApplicationState()));
}
