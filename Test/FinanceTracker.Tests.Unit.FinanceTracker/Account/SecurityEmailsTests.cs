using FinanceTracker.Components.Account;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Account;

public class SecurityEmailsTests
{
    private static readonly MailMessage Email = new("sam@example.test", "Two-factor", "text", "html");

    private readonly IMailSender _mailSender = Substitute.For<IMailSender>();
    private readonly IAccountEmails _accountEmails = Substitute.For<IAccountEmails>();

    public SecurityEmailsTests()
    {
        _mailSender.IsAvailable.Returns(true);
        _accountEmails.TwoFactorOnAsync("sam@example.test").Returns(Email);
        _accountEmails.TwoFactorOffAsync("sam@example.test", Arg.Any<TwoFactorOffCause>()).Returns(Email);
        _accountEmails.RecoveryCodesMadeAsync("sam@example.test").Returns(Email);
    }

    private SecurityEmails Subject => new(_mailSender, _accountEmails, NullLogger<SecurityEmails>.Instance);

    [Test]
    public async Task TwoFactorOn_ConfirmedEmail_SendsIt()
    {
        // Act
        await Subject.TwoFactorOnAsync(User(emailConfirmed: true));

        // Assert
        await _mailSender.Received(1).SendAsync(Email, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TwoFactorOff_ByAnAdmin_SaysSo()
    {
        // Act
        await Subject.TwoFactorOffAsync(User(emailConfirmed: true), TwoFactorOffCause.TurnedOffByAdmin);

        // Assert
        using (Assert.Multiple())
        {
            await _accountEmails.Received(1).TwoFactorOffAsync("sam@example.test", TwoFactorOffCause.TurnedOffByAdmin);
            await _mailSender.Received(1).SendAsync(Email, Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task RecoveryCodesMade_UnconfirmedEmail_SendsNothing()
    {
        // Act, an unconfirmed email might not be theirs
        await Subject.RecoveryCodesMadeAsync(User(emailConfirmed: false));

        // Assert
        await _mailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Test]
    public async Task TwoFactorOn_NoMailServer_SendsNothing()
    {
        // Arrange
        _mailSender.IsAvailable.Returns(false);

        // Act
        await Subject.TwoFactorOnAsync(User(emailConfirmed: true));

        // Assert
        await _mailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Test]
    public async Task TwoFactorOn_SendingFails_DoesNotThrow()
    {
        // Arrange, the change it's about has been made, so it mustn't fail
        _mailSender.SendAsync(Email, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("The mail server refused it"));

        // Act & Assert
        await Assert.That(() => Subject.TwoFactorOnAsync(User(emailConfirmed: true))).ThrowsNothing();
    }

    /// <summary>
    /// Sam, with their email confirmed or not.
    /// </summary>
    private static FinanceTrackerUser User(bool emailConfirmed) =>
        new("Sam", "Smith", "sam@example.test", "sam") { EmailConfirmed = emailConfirmed };
}
