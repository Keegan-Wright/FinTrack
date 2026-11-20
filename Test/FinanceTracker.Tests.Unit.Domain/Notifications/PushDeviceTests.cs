using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Tests.Unit.Domain.Notifications;

[Category("Domain")]
public class PushDeviceTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task PushDevice_Added_KeepsWhatTheBrowserGave_NameTrimmed()
    {
        // Act
        PushDevice device = new(Guid.NewGuid(), "https://push.example.test/1", "key", "secret", "  Chrome on Android ");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(device.Endpoint).IsEqualTo("https://push.example.test/1");
            await Assert.That(device.PublicKey).IsEqualTo("key");
            await Assert.That(device.AuthSecret).IsEqualTo("secret");
            await Assert.That(device.Name).IsEqualTo("Chrome on Android");
            await Assert.That(device.LastPushAt).IsNull();
        }
    }

    [Test]
    public async Task PushDevice_NameTooLong_IsRefused()
    {
        // Act, Assert
        await Assert.That(() => new PushDevice(Guid.NewGuid(), "https://push.example.test/1", "key", "secret",
            new string('a', PushDevice.MostNameLength + 1))).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Renew_TakesTheNewKeysAndName_KeepsTheEndpoint()
    {
        // Arrange
        PushDevice device = new(Guid.NewGuid(), "https://push.example.test/1", "key", "secret", "Chrome");

        // Act
        device.Renew("new key", "new secret", "Chrome on Android");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(device.Endpoint).IsEqualTo("https://push.example.test/1");
            await Assert.That(device.PublicKey).IsEqualTo("new key");
            await Assert.That(device.AuthSecret).IsEqualTo("new secret");
            await Assert.That(device.Name).IsEqualTo("Chrome on Android");
        }
    }

    [Test]
    public async Task RecordPush_FailedThenDelivered_ClearsTheFailure()
    {
        // Arrange
        PushDevice device = new(Guid.NewGuid(), "https://push.example.test/1", "key", "secret", "Chrome");
        device.RecordPush(Now, " Couldn't reach it ");

        // Act
        string? failed = device.LastPushFailure;
        device.RecordPush(Now.AddMinutes(1), "  ");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(failed).IsEqualTo("Couldn't reach it");
            await Assert.That(device.LastPushAt).IsEqualTo(Now.AddMinutes(1));
            await Assert.That(device.LastPushFailure).IsNull();
        }
    }
}
