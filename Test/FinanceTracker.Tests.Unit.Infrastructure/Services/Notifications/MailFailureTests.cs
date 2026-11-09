using System.Net.Sockets;
using FinanceTracker.Infrastructure.Notifications.Email;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class MailFailureTests
{
    [Test]
    public async Task Describe_SignInRefused_SaysSo() =>
        await Assert.That(MailFailure.Describe(new AuthenticationException("535 5.7.8 bad credentials at mx1")))
            .IsEqualTo("The mail server didn't accept FinTrack's sign in.");

    [Test]
    public async Task Describe_AddressRefused_SaysWhichAddress() =>
        await Assert.That(MailFailure.Describe(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted,
                SmtpStatusCode.MailboxUnavailable, "550 no such user")))
            .IsEqualTo("The mail server didn't accept your email address.");

    [Test]
    public async Task Describe_ServerUnreachable_SaysSo() =>
        await Assert.That(MailFailure.Describe(new SocketException((int)SocketError.ConnectionRefused)))
            .IsEqualTo("FinTrack couldn't reach the mail server.");

    [Test]
    public async Task Describe_Unexpected_NeverRepeatsItsMessage() =>
        await Assert.That(MailFailure.Describe(new InvalidOperationException("secret.mail.internal:25")))
            .DoesNotContain("secret");
}
