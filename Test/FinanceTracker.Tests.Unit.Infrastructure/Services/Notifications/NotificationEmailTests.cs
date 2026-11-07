using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class NotificationEmailTests
{
    private static readonly Notification Ended = new(Guid.NewGuid(), NotificationKind.ConsentEnded, "key",
        "Bank <one> access has ended", "FinTrack can no longer sync Bank <one> & co.", "ProviderSetup");

    [Test]
    public async Task For_AppAddressKnown_LinksToWhereItsDealtWithAndTheSettings()
    {
        // Act
        MailMessage email = NotificationEmail.For(Ended, "sam@example.test",
            new Uri("https://fintrack.example.test/money/"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.To).IsEqualTo("sam@example.test");
            await Assert.That(email.Subject).IsEqualTo("Bank <one> access has ended");
            await Assert.That(email.Text).Contains("Open it in FinTrack: https://fintrack.example.test/money/ProviderSetup");
            await Assert.That(email.Text).Contains("https://fintrack.example.test/money/Notifications");
            await Assert.That(email.Html).Contains("href=\"https://fintrack.example.test/money/ProviderSetup\"");
        }
    }

    [Test]
    public async Task For_AppAddressUnknown_SaysToOpenFinTrack()
    {
        // Act
        MailMessage email = NotificationEmail.For(Ended, "sam@example.test", null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Text).Contains("Open FinTrack to see it.");
            await Assert.That(email.Html).DoesNotContain("href=");
        }
    }

    [Test]
    public async Task For_TextInHtml_IsEscaped()
    {
        // Act
        MailMessage email = NotificationEmail.For(Ended, "sam@example.test", null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Html).Contains("Bank &lt;one&gt; &amp; co.");
            await Assert.That(email.Html).DoesNotContain("<one>");
        }
    }
}
