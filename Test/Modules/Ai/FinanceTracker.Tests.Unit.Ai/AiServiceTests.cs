using FinanceTracker.Ai.Contracts;

namespace FinanceTracker.Tests.Unit.Ai;

public class AiServiceTests : AiServiceTestBase
{
    private const string Address = "http://ollama.example.test:11434";


    [Test]
    public async Task GetStatusAsync_NothingSetUp_IsNotAvailableAndNotOptedIn()
    {
        // Act
        AiStatus status = await Service().GetStatusAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Available).IsFalse();
            await Assert.That(status.OptedIn).IsFalse();
        }
    }

    [Test]
    public async Task GetStatusAsync_EnvironmentGivesAnAddress_IsAvailable()
    {
        // Act
        AiStatus status = await Service(environmentEndpoint: Address).GetStatusAsync(CancellationToken);

        // Assert
        await Assert.That(status.Available).IsTrue();
    }

    [Test]
    public async Task ChangeOptInAsync_TurnsItOnForThatUserOnly()
    {
        // Arrange
        Guid other = Guid.NewGuid();

        // Act
        AiStatus status = await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);

        // Assert
        CurrentUser.UserId = other;
        AiStatus otherStatus = await Service().GetStatusAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(status.OptedIn).IsTrue();
            await Assert.That(otherStatus.OptedIn).IsFalse();
        }
    }

    [Test]
    public async Task ChangeOptInAsync_TurnedOnThenOff_IsOff()
    {
        // Arrange
        await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);

        // Act
        AiStatus status = await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = false }, CancellationToken);

        // Assert
        await Assert.That(status.OptedIn).IsFalse();
    }
}
