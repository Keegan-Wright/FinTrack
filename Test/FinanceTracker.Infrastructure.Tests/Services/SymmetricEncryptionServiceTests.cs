using System.Security.Cryptography;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Tests.Shared;

namespace FinanceTracker.Infrastructure.Tests.Services;

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


    [Test]
    public async Task EndToEnd_String_Success()
    {
        // Arrange
        var testString = "Im a test string";

        // Act
        var encryptedString = _subject.Encrypt(testString);
        var decryptedString = _subject.Decrypt<string>(encryptedString);

        // Assert
        await Assert.That(decryptedString).IsEqualTo(testString);
    }

    [Test]
    public async Task EndToEnd_Numeric_Success()
    {
        // Arrange
        var testInt = 1002;
        var testFloat = 1002f;
        var testDouble = 1002D;
        var testDecimal = 1002M;


        // Act
        var encryptedInt = _subject.Encrypt(testInt);
        var encryptedFloat = _subject.Encrypt(testFloat);
        var encryptedDouble = _subject.Encrypt(testDouble);
        var encryptedDecimal = _subject.Encrypt(testDecimal);

        var decryptedInt = _subject.Decrypt<int>(encryptedInt);
        var decryptedFloat = _subject.Decrypt<float>(encryptedFloat);
        var decryptedDouble = _subject.Decrypt<double>(encryptedDouble);
        var decryptedDecimal = _subject.Decrypt<decimal>(encryptedDecimal);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decryptedInt).IsEqualTo(testInt);
            await Assert.That(decryptedFloat).IsEqualTo(testFloat);
            await Assert.That(decryptedDouble).IsEqualTo(testDouble);
            await Assert.That(decryptedDecimal).IsEqualTo(testDecimal);
        }
    }

    [Test]
    public async Task EndToEnd_DateTime_Success()
    {
        // Arrange
        var testDateTime = DateTime.UtcNow;

        // Act
        var encryptedDateTime = _subject.Encrypt(testDateTime);

        var decryptedDateTime = _subject.Decrypt<DateTime>(encryptedDateTime);
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decryptedDateTime.Year).IsEquivalentTo(testDateTime.Year);
            await Assert.That(decryptedDateTime.Month).IsEquivalentTo(testDateTime.Month);
            await Assert.That(decryptedDateTime.Day).IsEquivalentTo(testDateTime.Day);

            await Assert.That(decryptedDateTime.Hour).IsEquivalentTo(testDateTime.Hour);
            await Assert.That(decryptedDateTime.Minute).IsEquivalentTo(testDateTime.Minute);
            await Assert.That(decryptedDateTime.Second).IsEquivalentTo(testDateTime.Second);
        }
    }
}
