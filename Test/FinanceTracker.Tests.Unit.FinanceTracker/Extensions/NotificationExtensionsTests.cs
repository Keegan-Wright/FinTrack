using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Extensions;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class NotificationExtensionsTests
{
    [Test]
    public async Task GetMailSettings_NoMailServer_SendsNoEmail()
    {
        // Act
        MailSettings settings = Configuration([]).GetMailSettings();

        // Assert
        await Assert.That(settings.IsConfigured).IsFalse();
    }

    [Test]
    public async Task GetMailSettings_MailServerWithoutFrom_SaysWhatsMissing()
    {
        // Arrange
        IConfiguration configuration = Configuration(new() { [FinTrackEnvironment.SmtpHost] = "mail.example.test" });

        // Act, Assert
        InvalidOperationException? exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(configuration.GetMailSettings()));
        await Assert.That(exception!.Message).Contains(FinTrackEnvironment.SmtpFrom);
    }

    [Test]
    public async Task GetMailSettings_EverythingSet_ReadsIt()
    {
        // Arrange
        IConfiguration configuration = Configuration(new()
        {
            [FinTrackEnvironment.SmtpHost] = "mail.example.test",
            [FinTrackEnvironment.SmtpPort] = "465",
            [FinTrackEnvironment.SmtpSecurity] = "SslOnConnect",
            [FinTrackEnvironment.SmtpUsername] = "fintrack",
            [FinTrackEnvironment.SmtpPassword] = "placeholder",
            [FinTrackEnvironment.SmtpFrom] = "FinTrack <fintrack@example.test>"
        });

        // Act
        MailSettings settings = configuration.GetMailSettings();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.IsConfigured).IsTrue();
            await Assert.That(settings.Port).IsEqualTo(465);
            await Assert.That(settings.Security).IsEqualTo(MailSecurity.SslOnConnect);
            await Assert.That(settings.Username).IsEqualTo("fintrack");
            await Assert.That(settings.From).IsEqualTo("FinTrack <fintrack@example.test>");
        }
    }

    [Test]
    public async Task GetMailSettings_PortAndSecurityNotSet_UsesTheDefaults()
    {
        // Act
        MailSettings settings = Configuration(new()
        {
            [FinTrackEnvironment.SmtpHost] = "mail.example.test",
            [FinTrackEnvironment.SmtpFrom] = "fintrack@example.test"
        }).GetMailSettings();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.Port).IsEqualTo(587);
            await Assert.That(settings.Security).IsEqualTo(MailSecurity.Auto);
        }
    }

    [Test]
    public async Task GetPublicAddress_Set_EndsInASlash() =>
        await Assert.That(Configuration(new() { [FinTrackEnvironment.PublicUrl] = "https://fintrack.example.test" })
                .GetPublicAddress().Url)
            .IsEqualTo(new Uri("https://fintrack.example.test/"));

    [Test]
    public async Task GetPublicAddress_EmptyAsComposePassesIt_LinksToNothing() =>
        await Assert.That(Configuration(new() { [FinTrackEnvironment.PublicUrl] = "" }).GetPublicAddress()
            .LinkTo("Notifications")).IsNull();

    [Test]
    public async Task GetPublicAddress_WithASubPath_KeepsItForLinks()
    {
        // Act
        PublicAddress address = Configuration(new()
        {
            [FinTrackEnvironment.PublicUrl] = "https://example.test/fintrack"
        }).GetPublicAddress();

        // Assert
        await Assert.That(address.LinkTo("Notifications")!.ToString())
            .IsEqualTo("https://example.test/fintrack/Notifications");
    }

    [Test]
    public async Task GetWebhookSettings_NotSet_RefusesPrivateAddresses() =>
        await Assert.That(Configuration([]).GetWebhookSettings().AllowPrivateAddresses).IsFalse();

    [Test]
    public async Task GetWebhookSettings_AllowedAsComposePassesIt_AllowsPrivateAddresses() =>
        await Assert.That(Configuration(new() { [FinTrackEnvironment.WebhookAllowPrivate] = "true" })
            .GetWebhookSettings().AllowPrivateAddresses).IsTrue();

    [Test]
    public async Task AddFinTrackNotifications_WebhookClient_PostsOnceWithoutTheDefaultsRetries()
    {
        // Arrange, the app's clients as it registers them, a webhook that always fails
        StubTrueLayerHandler webhook = new(_ =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)));
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddServiceDefaults();
        builder.AddFinTrackNotifications();
        builder.Services.AddHttpClient(WebhookSender.ClientName).ConfigurePrimaryHttpMessageHandler(() => webhook);
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        using HttpClient client = services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(WebhookSender.ClientName);

        // Act
        using HttpResponseMessage response = await client.PostAsync("https://hooks.example.test/fintrack", null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.InternalServerError);
            await Assert.That(webhook.Calls).IsEqualTo(1);
            await Assert.That(client.Timeout).IsEqualTo(WebhookSender.Timeout);
        }
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
