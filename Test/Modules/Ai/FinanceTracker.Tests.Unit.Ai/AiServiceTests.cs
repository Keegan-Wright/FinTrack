using FinanceTracker.Ai.Contracts;
using FinanceTracker.Ai.Persistence;
using Microsoft.EntityFrameworkCore;

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

    [Test]
    public async Task SaveSettingsAsync_SavesTheAddressTrimmedAndEncrypted()
    {
        // Act
        AiSettings settings = await Service()
            .SaveSettingsAsync(new SaveAiSettings { OllamaEndpoint = $"  {Address} " }, CancellationToken);

        // Assert
        await using FinanceTrackerAiContext context = ContextFactory.CreateDbContext();
        string stored = await context.Database
            .SqlQuery<string>($"SELECT \"Value\" AS \"Value\" FROM \"Settings\"")
            .SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(settings.OllamaEndpoint).IsEqualTo(Address);
            await Assert.That(stored).StartsWith("1:");
            await Assert.That(stored).DoesNotContain("ollama");
        }
    }

    [Test]
    public async Task SaveSettingsAsync_EmptyAddress_GoesBackToTheEnvironments()
    {
        // Arrange
        await Service().SaveSettingsAsync(new SaveAiSettings { OllamaEndpoint = Address }, CancellationToken);

        // Act
        AiSettings settings = await Service(environmentEndpoint: "http://env.example.test:11434")
            .SaveSettingsAsync(new SaveAiSettings { OllamaEndpoint = " " }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.OllamaEndpoint).IsNull();
            await Assert.That(settings.EnvironmentOllamaEndpoint).IsEqualTo("http://env.example.test:11434");
        }
    }

    [Test]
    [Arguments("http://ollama:11434", true)]
    [Arguments("https://ollama.example.test", true)]
    [Arguments("ftp://ollama:11434", false)]
    [Arguments("ollama:11434", false)]
    [Arguments("not an address", false)]
    public async Task IsValid_OnlyAbsoluteHttpAndHttps(string endpoint, bool valid) =>
        await Assert.That(OllamaEndpoint.IsValid(endpoint)).IsEqualTo(valid);
}
