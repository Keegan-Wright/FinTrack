using System.Security.Cryptography;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Tests.Unit.Shared;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class SymmetricEncryptionServiceTests : UnitTestBase
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
        string testString = "ValueToEncrypt";

        // Act
        string encryptedString = _subject.Encrypt(testString);

        // Assert
        await Assert.That(encryptedString).IsNotEqualTo(testString);
    }

    [Test]
    public async Task Encrypt_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        string testString = string.Empty;

        // Act
        string encryptedString = _subject.Encrypt(testString);

        // Assert
        await Assert.That(encryptedString).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Encrypt_NullString_ReturnsEmptyString()
    {
        // Arrange
        string? testString = null;

        // Act
        string encryptedString = _subject.Encrypt(testString);

        // Assert
        await Assert.That(encryptedString).IsEqualTo(string.Empty);
    }



    [Test]
    public async Task Decrypt_NullString_ReturnsDefault()
    {
        // Arrange
        string? testString = null;

        // Act
        string? decrypted = _subject.Decrypt<string>(testString!);

        // Assert
        await Assert.That(decrypted).IsNull();
    }

    [Test]
    public async Task Decrypt_UnencryptedString_ThrowsCryptoGraphicException()
    {
        // Arrange
        string testString = "IShouldThrow";

        // Act &  Assert
        await Assert.That(() => _subject.Decrypt<string>(testString))
            .Throws<CryptographicException>()
            .WithMessage("The input data is not a complete block.");
    }

    [Test]
    public async Task Decrypt_StringWithInvalidPadding_ThrowsCryptographicException()
    {
        // Arrange
        string testString = "IShouldThrow";

        string encryptedTestString = _subject.Encrypt(testString);

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
        string testString = "Im a test string";

        // Act
        string encryptedString = _subject.Encrypt(testString);
        string? decryptedString = _subject.Decrypt<string>(encryptedString);

        // Assert
        await Assert.That(decryptedString).IsEqualTo(testString);
    }

    [Test]
    public async Task EndToEnd_Numeric_Success()
    {
        // Arrange
        int testInt = 1002;
        float testFloat = 1002f;
        double testDouble = 1002D;
        decimal testDecimal = 1002M;


        // Act
        string encryptedInt = _subject.Encrypt(testInt);
        string encryptedFloat = _subject.Encrypt(testFloat);
        string encryptedDouble = _subject.Encrypt(testDouble);
        string encryptedDecimal = _subject.Encrypt(testDecimal);

        int decryptedInt = _subject.Decrypt<int>(encryptedInt);
        float decryptedFloat = _subject.Decrypt<float>(encryptedFloat);
        double decryptedDouble = _subject.Decrypt<double>(encryptedDouble);
        decimal decryptedDecimal = _subject.Decrypt<decimal>(encryptedDecimal);

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
        DateTime testDateTime = DateTime.UtcNow;

        // Act
        string encryptedDateTime = _subject.Encrypt(testDateTime);

        DateTime decryptedDateTime = _subject.Decrypt<DateTime>(encryptedDateTime);
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

    [Test]
    public async Task EndToEnd_DateTimeOffset_KeepsOffset()
    {
        // Arrange
        var testDateTimeOffset = new DateTimeOffset(2026, 3, 4, 5, 6, 7, 890, TimeSpan.FromHours(2));

        // Act
        DateTimeOffset decrypted = _subject.Decrypt<DateTimeOffset>(_subject.Encrypt(testDateTimeOffset));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decrypted).IsEqualTo(testDateTimeOffset);
            await Assert.That(decrypted.Offset).IsEqualTo(TimeSpan.FromHours(2));
        }
    }

    [Test]
    public async Task EndToEnd_Array_Success()
    {
        // Arrange
        int[] testArray = [1, 2, 3];

        // Act
        int[]? decrypted = _subject.Decrypt<int[]>(_subject.Encrypt(testArray));

        // Assert
        await Assert.That(decrypted).IsEquivalentTo(testArray);
    }

    [Test]
    public async Task EndToEnd_Collection_Success()
    {
        // Arrange
        List<string> testList = ["a", "b"];

        // Act
        List<string>? decrypted = _subject.Decrypt<List<string>>(_subject.Encrypt(testList));

        // Assert
        await Assert.That(decrypted).IsEquivalentTo(testList);
    }

    [Test]
    public async Task EndToEnd_Enum_Success()
    {
        // Act
        DayOfWeek decrypted = _subject.Decrypt<DayOfWeek>(_subject.Encrypt(DayOfWeek.Friday));

        // Assert
        await Assert.That(decrypted).IsEqualTo(DayOfWeek.Friday);
    }

    [Test]
    public async Task EndToEnd_NullableValue_Success()
    {
        // Act
        int? decrypted = _subject.Decrypt<int?>(_subject.Encrypt<int?>(42));

        // Assert
        await Assert.That(decrypted).IsEqualTo(42);
    }

    [Test]
    public async Task Decrypt_NullJsonArray_ReturnsEmptyArray()
    {
        // Act
        int[]? decrypted = _subject.Decrypt<int[]>(_subject.Encrypt("null"));

        // Assert
        await Assert.That(decrypted).IsEquivalentTo(Array.Empty<int>());
    }

    [Test]
    public async Task Decrypt_NullJsonCollection_Throws()
    {
        await Assert.That(() => _subject.Decrypt<List<int>>(_subject.Encrypt("null")))
            .Throws<InvalidOperationException>();
    }
}
