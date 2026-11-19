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
            await Assert.That(email.Html)
                .Contains("href=\"https://fintrack.example.test/money/Account/ResetPassword?code=abc&amp;x=1\"");
            await Assert.That(email.Html)
                .Contains(">https://fintrack.example.test/money/Account/ResetPassword?code=abc&amp;x=1</a>");
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

    [Test]
    public async Task TwoFactorOn_LinksToTheProfileAndSaysWhatToDoIfItWasntThem()
    {
        // Act
        MailMessage email = await TestEmails.Account(PublicUrl).TwoFactorOnAsync("sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.TwoFactorOnSubject);
            await Assert.That(email.Html).Contains("Two-factor sign in is on");
            await Assert.That(email.Html).Contains("href=\"https://fintrack.example.test/money/YourProfile\"");
            await Assert.That(email.Text).Contains("Open your profile: https://fintrack.example.test/money/YourProfile");
            await Assert.That(email.Text).Contains("If that wasn't you, change your password straight away");
        }
    }

    [Test]
    [Arguments(TwoFactorOffCause.TurnedOff, "was turned off")]
    [Arguments(TwoFactorOffCause.AuthenticatorReset, "Your authenticator was reset")]
    [Arguments(TwoFactorOffCause.TurnedOffByAdmin, "Your FinTrack admin turned off two-factor sign in")]
    public async Task TwoFactorOff_SaysHow(TwoFactorOffCause cause, string expected)
    {
        // Act
        MailMessage email = await TestEmails.Account(PublicUrl).TwoFactorOffAsync("sam@example.test", cause);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.TwoFactorOffSubject);
            await Assert.That(email.Text).Contains(expected);
        }
    }

    [Test]
    public async Task RecoveryCodesMade_WithoutAPublicAddress_HasNoButton()
    {
        // Act
        MailMessage email = await TestEmails.Account().RecoveryCodesMadeAsync("sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.RecoveryCodesMadeSubject);
            await Assert.That(email.Text).Contains("The old ones no longer work");
            await Assert.That(email.Text).DoesNotContain("Open your profile");
            await Assert.That(email.Html).DoesNotContain("If the button doesn");
        }
    }

    [Test]
    public async Task PasskeyAdded_NamesItAndSaysItSignsInWithoutAPassword()
    {
        // Act
        MailMessage email = await TestEmails.Account(PublicUrl).PasskeyAddedAsync("sam@example.test", "Work laptop");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.PasskeyAddedSubject);
            await Assert.That(email.Text).Contains("\"Work laptop\" was added");
            await Assert.That(email.Text).Contains("If that wasn't you, change your password straight away");
        }
    }

    [Test]
    public async Task PasskeysRemovedByAdmin_SaysTheAdminDidIt()
    {
        // Act
        MailMessage email = await TestEmails.Account(PublicUrl).PasskeysRemovedByAdminAsync("sam@example.test");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(email.Subject).IsEqualTo(AccountEmails.PasskeyRemovedSubject);
            await Assert.That(email.Text).Contains("Your FinTrack admin removed all your passkeys");
        }
    }

    [Test]
    public async Task PasskeyAdded_ANameWithMarkup_IsShownAsText()
    {
        // Act
        MailMessage email = await TestEmails.Account(PublicUrl).PasskeyAddedAsync("sam@example.test", "<b>Laptop</b>");

        // Assert
        await Assert.That(email.Html).DoesNotContain("<b>Laptop</b>");
    }
}
