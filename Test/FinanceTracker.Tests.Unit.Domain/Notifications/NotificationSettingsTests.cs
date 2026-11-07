using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Tests.Unit.Domain.Notifications;

[Category("Domain")]
public class NotificationSettingsTests
{
    [Test]
    public async Task NotificationSettings_New_EmailsEveryKind()
    {
        // Act
        NotificationSettings settings = new(Guid.NewGuid());

        // Assert
        await Assert.That(settings.Emailed).IsEquivalentTo(Enum.GetValues<NotificationKind>());
    }

    [Test]
    public async Task EmailOnly_SomeKindsRepeated_EmailsJustThoseOnce()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act
        settings.EmailOnly([NotificationKind.SyncFailed, NotificationKind.SyncFailed, NotificationKind.ConsentEnded]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.Emailed).IsEquivalentTo([NotificationKind.ConsentEnded, NotificationKind.SyncFailed]);
            await Assert.That(settings.IsEmailed(NotificationKind.ConsentEndingSoon)).IsFalse();
        }
    }

    [Test]
    public async Task IsEmailedFor_NeverChanged_EmailsEveryKind()
    {
        // Act, Assert
        await Assert.That(Enum.GetValues<NotificationKind>()
            .All(static x => NotificationSettings.IsEmailedFor(null, x))).IsTrue();
    }
}
