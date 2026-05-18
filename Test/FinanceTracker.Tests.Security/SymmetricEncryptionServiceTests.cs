using System.Security.Cryptography;
using FinanceTracker.Configurations;
using FinanceTracker.Security.Encryption;
using FinanceTracker.Tests.Shared;

namespace FinanceTracker.Tests.Security;

public class SymmetricEncryptionServiceTests : TestFixtureBase
{
    private readonly SymmetricEncryptionService _subject = new(new EncryptionConfiguration()
    {
        SymmetricKey = "SymmetricKeyForTests",
        Iterations = 1000,
        SymmetricSalt = "SymmetricSaltForTests"
    });

    [Test]
    public async Task Encrypt_ValidValue_ReturnsEncryptedValue()
    {
        // Arrange
        var testString = "ValueToEncrypt";

        // Act
        var encryptedString = _subject.Encrypt(testString);

        // Assert
        await Assert.That(encryptedString).IsNotEqualTo(testString);
    }

    [Test]
    public async Task Encrypt_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        var testString = string.Empty;

        // Act
        var encryptedString = _subject.Encrypt(testString);

        // Assert
        await Assert.That(encryptedString).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Encrypt_NullString_ReturnsEmptyString()
    {
        // Arrange
        string? testString = null;

        // Act
        var encryptedString = _subject.Encrypt(testString);

        // Assert
        await Assert.That(encryptedString).IsEqualTo(string.Empty);
    }



    [Test]
    public async Task Decrypt_NullString_ReturnsDefault()
    {
        // Arrange
        string? testString = null;

        // Act
        var encryptedString = _subject.Decrypt<string>(testString);

        // Assert
        await Assert.That(encryptedString).IsEqualTo((string)default);
    }

    [Test]
    public async Task Decrypt_UnencryptedString_ThrowsCryptoGraphicException()
    {
        // Arrange
        var testString = "IShouldThrow";

        // Act &  Assert
        await Assert.That(() => _subject.Decrypt<string>(testString))
            .Throws<CryptographicException>()
            .WithMessage("The input data is not a complete block.");
    }

    [Test]
    public async Task Decrypt_StringWithInvalidPadding_ThrowsCryptographicException()
    {
        // Arrange
        var testString = "IShouldThrow";

        var encryptedTestString = _subject.Encrypt(testString);

        var inlineDecryptor = new SymmetricEncryptionService(new EncryptionConfiguration()
        {
            Iterations = 10000,
            SymmetricKey = "NewKey",
            SymmetricSalt = "NewSalt"
        });

        // Act &  Assert
        await Assert.That(() => inlineDecryptor.Decrypt<string>(encryptedTestString))
            .Throws<CryptographicException>()
            .WithMessage("Padding is invalid and cannot be removed.");
    }
}
