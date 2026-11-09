using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// Why an email couldn't be sent, in words for the Notifications page. Says what went wrong without the mail server's
/// own message, which can name the server and how it's set up.
/// </summary>
public static class MailFailure
{
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is AuthenticationException)
        {
            return "The mail server didn't accept FinTrack's sign in.";
        }

        if (exception is SslHandshakeException)
        {
            return "FinTrack couldn't make a secure connection to the mail server.";
        }

        if (exception is SmtpCommandException command)
        {
            return DescribeRefusal(command.ErrorCode);
        }

        if (exception is SocketException or SmtpProtocolException or TimeoutException or IOException)
        {
            return "FinTrack couldn't reach the mail server.";
        }

        return "Something went wrong sending it.";
    }

    private static string DescribeRefusal(SmtpErrorCode errorCode)
    {
        if (errorCode == SmtpErrorCode.RecipientNotAccepted)
        {
            return "The mail server didn't accept your email address.";
        }

        if (errorCode == SmtpErrorCode.SenderNotAccepted)
        {
            return "The mail server didn't accept the address FinTrack sends from.";
        }

        return "The mail server didn't accept the email.";
    }
}
