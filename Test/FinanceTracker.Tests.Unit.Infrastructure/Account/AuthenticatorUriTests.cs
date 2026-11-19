using FinanceTracker.Infrastructure.Account.Authenticator;

namespace FinanceTracker.Tests.Unit.Infrastructure.Account;

public class AuthenticatorUriTests
{
    [Test]
    public async Task For_AnAccount_NamesSha256AndTheSettings()
    {
        // Act
        string uri = AuthenticatorUri.For("someone@example.com", "MZXW6YTBOI");

        // Assert
        await Assert.That(uri).IsEqualTo(
            "otpauth://totp/FinTrack:someone%40example.com?secret=MZXW6YTBOI&issuer=FinTrack" +
            "&algorithm=SHA256&digits=6&period=30");
    }
}
