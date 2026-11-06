using FinanceTracker.Client;

namespace FinanceTracker.Tests.Unit.Client;

public class AppUpdateTests
{
    private const string Running = "0.1.1+3e871e2";

    [Test]
    [Arguments(Running)]
    [Arguments(null)]
    [Arguments("")]
    public async Task ServerRuns_ThisBuildOrDoesNotSay_IsNotAnUpdate(string? version)
    {
        // Arrange
        AppUpdate update = new(Running);
        int raised = 0;
        update.AvailableChanged += (_, _) => raised++;

        // Act
        update.ServerRuns(version);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(update.Available).IsFalse();
            await Assert.That(raised).IsEqualTo(0);
        }
    }

    [Test]
    public async Task ServerRuns_AnotherBuild_IsAnUpdateSaidOnce()
    {
        // Arrange
        AppUpdate update = new(Running);
        int raised = 0;
        update.AvailableChanged += (_, _) => raised++;

        // Act, every answer after says so too
        update.ServerRuns("0.1.2+9a8b7c6");
        update.ServerRuns("0.1.2+9a8b7c6");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(update.Available).IsTrue();
            await Assert.That(raised).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ServerRuns_ThisBuildAgainAfterAnother_StillAnUpdate()
    {
        // Arrange, e.g. an update rolled back, the API this tab was loaded against may still have changed twice
        AppUpdate update = new(Running);
        update.ServerRuns("0.1.2+9a8b7c6");

        // Act
        update.ServerRuns(Running);

        // Assert
        await Assert.That(update.Available).IsTrue();
    }
}
