using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Account;

public class AccountEmailsTests
{
    private static readonly Uri PublicUrl = new("https://fintrack.example.test/money/");
    private static readonly Uri Link = new("https://fintrack.example.test/money/Account/ResetPassword?code=abc&x=1");

    [Test]
    public async Task ResetPassword_CarriesTheLinkAsAButtonAndWrittenOut()
    {
        // Act
        MailMessage email = await TestEmails.Account(PublicUrl).ResetPasswordAsync("sam@example.test", Link);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.To).IsEqualTo("sam@example.test");
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.ResetPasswordSubject);
            await Assert.That(email.Text).Contains($"Choose a new password: {Link}");
            await Assert.That(email.Html).Contains("Reset your password");
            await Assert.That(email.Html).Contains("Choose a new password");
            await Assert.That(email.Html).Contains("href=\"https://fintrack.example.test/money/Account/ResetPassword?code=abc&amp;x=1\"");
            await Assert.That(email.Html).Contains(">https://fintrack.example.test/money/Account/ResetPassword?code=abc&amp;x=1</a>");
            await Assert.That(email.Html).Contains("Sent by FinTrack at fintrack.example.test");
            await Assert.That(email.Images).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task ResetPassword_DoesNotOfferToChangeWhichEmailsTheyGet()
    {
        // Act
        MailMessage email = await TestEmails.Account(PublicUrl).ResetPasswordAsync("sam@example.test", Link);

        // Assert, an account's own emails can't be turned off
        using (Assert.Multiple())
        {
            await Assert.That(email.Html).DoesNotContain("change which emails you get");
            await Assert.That(email.Text).DoesNotContain("change which emails you get");
            await Assert.That(email.Html).Contains("If that wasn&#x27;t you, you can ignore it");
        }
    }

    [Test]
    public async Task ConfirmEmail_SaysWhatItsFor()
    {
        // Act
        MailMessage email = await TestEmails.Account().ConfirmEmailAsync("sam@example.test", Link);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.ConfirmEmailSubject);
            await Assert.That(email.Text).Contains("send you your notifications, and a link if you ever forget");
            await Assert.That(email.Text).Contains($"Confirm your email: {Link}");
            await Assert.That(email.Html).DoesNotContain("Sent by FinTrack at");
        }
    }

    [Test]
    public async Task ChangeEmail_SaysTheOldOneStaysUntilConfirmed()
    {
        // Act
        MailMessage email = await TestEmails.Account().ChangeEmailAsync("new@example.test", Link);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.To).IsEqualTo("new@example.test");
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.ChangeEmailSubject);
            await Assert.That(email.Text).Contains("Until then, it keeps the old one.");
            await Assert.That(email.Text).Contains($"Use this email: {Link}");
        }
    }
}
