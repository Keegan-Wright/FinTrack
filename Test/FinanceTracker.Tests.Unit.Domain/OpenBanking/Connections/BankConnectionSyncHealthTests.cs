using FinanceTracker.Domain.OpenBanking.Connections;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Connections;

[Category("Domain")]
public class BankConnectionSyncHealthTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task RecordSyncFailed_First_SaysItsNewAndWhenItStarted()
    {
        // Arrange
        BankConnection connection = Connection();

        // Act
        bool started = connection.RecordSyncFailed(Now);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(started).IsTrue();
            await Assert.That(connection.SyncFailingSince).IsEqualTo(Now);
        }
    }

    [Test]
    public async Task RecordSyncFailed_Again_IsNotNewAndKeepsWhenItStarted()
    {
        // Arrange
        BankConnection connection = Connection();
        connection.RecordSyncFailed(Now);

        // Act
        bool started = connection.RecordSyncFailed(Now.AddHours(4));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(started).IsFalse();
            await Assert.That(connection.SyncFailingSince).IsEqualTo(Now);
        }
    }

    [Test]
    public async Task RecordSyncFailed_AfterASuccess_IsNewAgain()
    {
        // Arrange
        BankConnection connection = Connection();
        connection.RecordSyncFailed(Now);
        connection.RecordSyncSucceeded();

        // Act
        bool started = connection.RecordSyncFailed(Now.AddHours(8));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(started).IsTrue();
            await Assert.That(connection.SyncFailingSince).IsEqualTo(Now.AddHours(8));
        }
    }

    private static BankConnection Connection() =>
        new(Guid.NewGuid(), "ob-bank", "Bank", "code", ["accounts"], [], Now.AddDays(-1), Now.AddDays(89));
}
