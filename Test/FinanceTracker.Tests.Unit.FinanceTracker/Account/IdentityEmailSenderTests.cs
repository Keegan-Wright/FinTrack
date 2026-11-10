using FinanceTracker.Components.Account;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Account;

public class IdentityEmailSenderTests
{
    private const string Link = "https://fintrack.example.test/Account/ResetPassword?code=abc";

    private static readonly FinanceTrackerUser User = new("Sam", "Smith", "sam@example.test", "sam");

    private readonly IMailSender _mailSender = Substitute.For<IMailSender>();
    private readonly IAccountEmails _emails = Substitute.For<IAccountEmails>();

    private IdentityEmailSender Subject => new(_mailSender, _emails, NullLogger<IdentityEmailSender>.Instance);

    [Test]
    public async Task SendPasswordResetLink_MailServerSetUp_SendsTheResetEmail()
    {
        // Arrange
        MailMessage email = new("sam@example.test", "Reset", "text", "html");
        _mailSender.IsAvailable.Returns(true);
        _emails.ResetPasswordAsync("sam@example.test", new Uri(Link)).Returns(email);

        // Act
        await Subject.SendPasswordResetLinkAsync(User, "sam@example.test", Link);

        // Assert
        await _mailSender.Received(1).SendAsync(email, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendConfirmationLink_MailServerSetUp_SendsTheConfirmEmail()
    {
        // Arrange
        MailMessage email = new("sam@example.test", "Confirm", "text", "html");
        _mailSender.IsAvailable.Returns(true);
        _emails.ConfirmEmailAsync("sam@example.test", new Uri(Link)).Returns(email);

        // Act
        await Subject.SendConfirmationLinkAsync(User, "sam@example.test", Link);

        // Assert
        await _mailSender.Received(1).SendAsync(email, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendPasswordResetLink_NoMailServer_SendsNothing()
    {
        // Arrange
        _mailSender.IsAvailable.Returns(false);

        // Act
        await Subject.SendPasswordResetLinkAsync(User, "sam@example.test", Link);

        // Assert
        await _mailSender.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendPasswordResetLink_SendingFails_Throws()
    {
        // Arrange
        _mailSender.IsAvailable.Returns(true);
        _emails.ResetPasswordAsync(Arg.Any<string>(), Arg.Any<Uri>())
            .Returns(new MailMessage("sam@example.test", "Reset", "text", "html"));
        _mailSender.SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Mail server down")));

        // Act and assert, for the page to decide what the user is told
        await Assert.That(() => Subject.SendPasswordResetLinkAsync(User, "sam@example.test", Link))
            .Throws<InvalidOperationException>();
    }
}
