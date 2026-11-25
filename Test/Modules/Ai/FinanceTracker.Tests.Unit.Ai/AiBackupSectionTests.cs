using FinanceTracker.Ai.Backup;
using FinanceTracker.Ai.Contracts;
using FinanceTracker.Infrastructure.Common.Exports;

namespace FinanceTracker.Tests.Unit.Ai;

public class AiBackupSectionTests : AiServiceTestBase
{
    [Test]
    public async Task ReadAsync_GivesTheUsersOptIn()
    {
        // Arrange
        await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);

        // Act
        BackupSectionData data = await new AiBackupSection(Service()).ReadAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(data.Data).IsEqualTo(new AiBackup(OptedIn: true));
            await Assert.That(data.Records).IsEqualTo(1);
            await Assert.That(new AiBackupSection(Service()).FileName).IsEqualTo("ai.json");
        }
    }
}
