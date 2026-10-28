using System.Security.Cryptography;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class ColumnEncryptionServiceTests : UnitTestBase
{
    private readonly ColumnEncryptionService _subject = new(TestEncryptionKeys.Keyring());

    [Test]
    public async Task Encrypt_ValidValue_IsPrefixedWithTheCurrentKeyAndHidesThePlaintext()
    {
        // Act
        string encrypted = _subject.Encrypt("ValueToEncrypt");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(encrypted).StartsWith("1:");
            await Assert.That(encrypted).DoesNotContain("ValueToEncrypt");
        }
    }

    [Test]
    public async Task Encrypt_SameValueTwice_GivesDifferentCiphertext()
    {
        // Act
        string first = _subject.Encrypt("Groceries");
        string second = _subject.Encrypt("Groceries");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsNotEqualTo(second);
            await Assert.That(_subject.Decrypt<string>(first)).IsEqualTo("Groceries");
            await Assert.That(_subject.Decrypt<string>(second)).IsEqualTo("Groceries");
        }
    }

    [Test]
    public async Task Encrypt_EmptyString_ReturnsEmptyString()
    {
        // Act & Assert
        await Assert.That(_subject.Encrypt(string.Empty)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Encrypt_NullString_ReturnsEmptyString()
    {
        // Arrange
        string? testString = null;

        // Act & Assert
        await Assert.That(_subject.Encrypt(testString)).IsEqualTo(string.Empty);
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
    public async Task Decrypt_EmptyCollection_ReturnsAnEmptyOne()
    {
        // Act
        IReadOnlyList<string>? decrypted = _subject.Decrypt<IReadOnlyList<string>>(string.Empty);

        // Assert
        await Assert.That(decrypted).IsNotNull().And.IsEmpty();
    }

    [Test]
    public async Task Decrypt_EmptyBytes_ReturnsDefault()
    {
        // Act
        byte[]? decrypted = _subject.Decrypt<byte[]>(string.Empty);

        // Assert
        await Assert.That(decrypted).IsNull();
    }

    [Test]
    [Arguments("NotEncrypted")]
    [Arguments("1:not-base64!")]
    [Arguments("x:AAAA")]
    [Arguments("1:AAAA")]
    public async Task Decrypt_NotInTheEncryptedFormat_Throws(string value)
    {
        // Act & Assert
        await Assert.That(() => _subject.Decrypt<string>(value)).Throws<CryptographicException>();
    }

    [Test]
    public async Task Decrypt_TamperedValue_Throws()
    {
        // Arrange
        string encrypted = _subject.Encrypt("Balance 100");
        byte[] payload = Convert.FromBase64String(encrypted[2..]);
        payload[^1] ^= 0x01;
        string tampered = "1:" + Convert.ToBase64String(payload);

        // Act & Assert
        await Assert.That(() => _subject.Decrypt<string>(tampered)).Throws<CryptographicException>();
    }

    [Test]
    public async Task Decrypt_KeyNotInTheKeyring_Throws()
    {
        // Arrange
        string encrypted = new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2)).Encrypt("secret");

        // Act & Assert
        await Assert.That(() => _subject.Decrypt<string>(encrypted))
            .Throws<CryptographicException>()
            .WithMessageContaining("key 2");
    }

    [Test]
    public async Task Decrypt_WithADifferentKeyUnderTheSameId_Throws()
    {
        // Arrange
        EncryptionConfiguration otherKey = new()
        {
            Keys = new Dictionary<int, byte[]> { [1] = TestEncryptionKeys.Key(99) },
            CurrentKeyId = 1
        };
        string encrypted = new ColumnEncryptionService(otherKey).Encrypt("secret");

        // Act & Assert
        await Assert.That(() => _subject.Decrypt<string>(encrypted)).Throws<CryptographicException>();
    }

    [Test]
    public async Task Decrypt_AfterRotation_ReadsValuesWrittenWithTheOldKey()
    {
        // Arrange
        string writtenWithKey1 = _subject.Encrypt("before rotation");
        ColumnEncryptionService rotated = new(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2));

        // Act
        string? decrypted = rotated.Decrypt<string>(writtenWithKey1);
        string writtenWithKey2 = rotated.Encrypt("after rotation");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decrypted).IsEqualTo("before rotation");
            await Assert.That(writtenWithKey2).StartsWith("2:");
        }
    }

    [Test]
    public async Task ComputeBlindIndex_IsStableForAValueAndDiffersBetweenValues()
    {
        // Act
        string first = _subject.ComputeBlindIndex("txn-1");
        string again = new ColumnEncryptionService(TestEncryptionKeys.Keyring()).ComputeBlindIndex("txn-1");
        string other = _subject.ComputeBlindIndex("txn-2");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(again).IsEqualTo(first);
            await Assert.That(other).IsNotEqualTo(first);
            await Assert.That(first).DoesNotContain("txn");
        }
    }

    [Test]
    public async Task ComputeBlindIndex_ChangesWithTheCurrentKey()
    {
        // Arrange
        ColumnEncryptionService rotated = new(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2));

        // Act & Assert
        await Assert.That(rotated.ComputeBlindIndex("txn-1")).IsNotEqualTo(_subject.ComputeBlindIndex("txn-1"));
    }

    [Test]
    public async Task KeyringFingerprint_MatchesForTheSameKeysOnly()
    {
        // Act
        string same = new ColumnEncryptionService(TestEncryptionKeys.Keyring()).KeyringFingerprint;
        string rotated = new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2)).KeyringFingerprint;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(same).IsEqualTo(_subject.KeyringFingerprint);
            await Assert.That(rotated).IsNotEqualTo(_subject.KeyringFingerprint);
        }
    }

    [Test]
    public async Task Constructor_CurrentKeyNotInTheKeyring_Throws()
    {
        // Arrange
        EncryptionConfiguration configuration = new()
        {
            Keys = new Dictionary<int, byte[]> { [1] = TestEncryptionKeys.Key(1) },
            CurrentKeyId = 2
        };

        // Act & Assert
        await Assert.That(() => new ColumnEncryptionService(configuration)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_KeyOfTheWrongLength_Throws()
    {
        // Arrange
        EncryptionConfiguration configuration = new()
        {
            Keys = new Dictionary<int, byte[]> { [1] = new byte[16] },
            CurrentKeyId = 1
        };

        // Act & Assert
        await Assert.That(() => new ColumnEncryptionService(configuration)).Throws<ArgumentException>();
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
