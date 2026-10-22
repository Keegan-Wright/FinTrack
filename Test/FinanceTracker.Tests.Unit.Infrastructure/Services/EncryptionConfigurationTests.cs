using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class EncryptionConfigurationTests
{
    [Test]
    public async Task Parse_KeyPairs_ReadsEveryKey()
    {
        // Arrange
        string key1 = Convert.ToBase64String(TestEncryptionKeys.Key(1));
        string key2 = Convert.ToBase64String(TestEncryptionKeys.Key(2));

        // Act
        EncryptionConfiguration configuration = EncryptionConfiguration.Parse($" 1:{key1} ; 2:{key2};", 2);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(configuration.CurrentKeyId).IsEqualTo(2);
            await Assert.That(configuration.Keys[1]).IsEquivalentTo(TestEncryptionKeys.Key(1));
            await Assert.That(configuration.Keys[2]).IsEquivalentTo(TestEncryptionKeys.Key(2));
        }
    }

    [Test]
    [Arguments("no-separator")]
    [Arguments("one:AAAA")]
    [Arguments("-1:AAAA")]
    [Arguments("1:AAAA;1:BBBB")]
    public async Task Parse_Malformed_Throws(string keys)
    {
        // Act & Assert
        await Assert.That(() => EncryptionConfiguration.Parse(keys, 1)).Throws<FormatException>();
    }

    [Test]
    public async Task GenerateKey_IsAFullLengthRandomKey()
    {
        // Act
        byte[] first = Convert.FromBase64String(EncryptionConfiguration.GenerateKey());
        byte[] second = Convert.FromBase64String(EncryptionConfiguration.GenerateKey());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Length).IsEqualTo(EncryptionConfiguration.KeySize);
            await Assert.That(first).IsNotEquivalentTo(second);
        }
    }
}
