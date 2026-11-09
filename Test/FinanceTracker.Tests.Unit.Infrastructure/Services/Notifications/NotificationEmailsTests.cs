using FinanceTracker.Design;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Emails;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class NotificationEmailsTests
{
    private static readonly Uri PublicUrl = new("https://fintrack.example.test/money/");

    private static readonly Notification Ended = new(Guid.NewGuid(), NotificationKind.ConsentEnded, "key",
        "Bank <one> access has ended", "FinTrack can no longer sync Bank <one> & co.", "ProviderSetup");

    [Test]
    public async Task For_AppAddressKnown_LinksToWhereItsDealtWithAndTheSettings()
    {
        // Act
        MailMessage email = await TestEmails.Create(PublicUrl).ForAsync(Ended, "sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.To).IsEqualTo("sam@example.test");
            await Assert.That(email.Subject).IsEqualTo("Bank <one> access has ended");
            await Assert.That(email.Text)
                .Contains("Open it in FinTrack: https://fintrack.example.test/money/ProviderSetup");
            await Assert.That(email.Text).Contains("https://fintrack.example.test/money/Notifications/Settings");
            await Assert.That(email.Html).Contains("href=\"https://fintrack.example.test/money/ProviderSetup\"");
            await Assert.That(email.Html).Contains("href=\"https://fintrack.example.test/money/Notifications/Settings\"");
            await Assert.That(email.Html).Contains("Sent by FinTrack at fintrack.example.test");
            await Assert.That(email.Html).Contains("notifications. You can");
        }
    }

    [Test]
    public async Task For_AppAddressUnknown_SaysToOpenFinTrack()
    {
        // Act
        MailMessage email = await TestEmails.Create().ForAsync(Ended, "sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Text).Contains("Open FinTrack to see it.");
            await Assert.That(email.Html).Contains("Open FinTrack to see it.");
            await Assert.That(email.Html).DoesNotContain("href=");
            await Assert.That(email.Html).DoesNotContain("Sent by FinTrack at");
        }
    }

    [Test]
    public async Task For_TextInHtml_IsEscaped()
    {
        // Act
        MailMessage email = await TestEmails.Create().ForAsync(Ended, "sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Html).Contains("Bank &lt;one&gt; &amp; co.");
            await Assert.That(email.Html).DoesNotContain("<one>");
        }
    }

    [Test]
    public async Task For_Any_CarriesTheLogoAndTheBrand()
    {
        // Act
        MailMessage email = await TestEmails.Create(PublicUrl).ForAsync(Ended, "sam@example.test");

        // Assert
        MailImage logo = email.Images.Single();
        using (Assert.Multiple())
        {
            await Assert.That(email.Html).StartsWith("<!DOCTYPE html>");
            await Assert.That(email.Html).Contains($"src=\"cid:{EmailBrand.LogoContentId}\"");
            await Assert.That(logo.ContentId).IsEqualTo(EmailBrand.LogoContentId);
            await Assert.That(logo.ContentType).IsEqualTo("image/png");
            // A PNG's signature
            await Assert.That(logo.Content.Span[1..4].ToArray()).IsEquivalentTo("PNG"u8.ToArray());
            await Assert.That(email.Html).Contains($"background-color: {LightPalette.Primary}");
        }
    }

    [Test]
    [Arguments(NotificationKind.ConsentEndingSoon, LightPalette.Warning)]
    [Arguments(NotificationKind.ConsentEnded, LightPalette.Error)]
    [Arguments(NotificationKind.SyncFailed, LightPalette.Error)]
    public async Task For_Kind_SaysWhatItsAboutInItsColour(NotificationKind kind, string accent)
    {
        // Arrange
        Notification notification = new(Guid.NewGuid(), kind, "key", "A title", "A message", null);

        // Act
        MailMessage email = await TestEmails.Create().ForAsync(notification, "sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Html).Contains($"border-top: 4px solid {accent}");
            await Assert.That(email.Html).Contains($">{NotificationKindStyle.Label(kind)}</p>");
        }
    }

    [Test]
    public async Task Test_AppAddressKnown_SaysEmailWorksAndLinksToTheSettings()
    {
        // Act
        MailMessage email = await TestEmails.Create(PublicUrl).TestAsync("sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.To).IsEqualTo("sam@example.test");
            await Assert.That(email.Subject).IsEqualTo(NotificationEmails.TestSubject);
            await Assert.That(email.Text).Contains("Email works.");
            await Assert.That(email.Html).Contains("Email works");
            await Assert.That(email.Html).Contains("href=\"https://fintrack.example.test/money/Notifications/Settings\"");
            await Assert.That(email.Images.Single().ContentId).IsEqualTo(EmailBrand.LogoContentId);
        }
    }
}
