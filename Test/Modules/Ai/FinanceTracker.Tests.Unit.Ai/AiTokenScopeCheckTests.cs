using FinanceTracker.Ai.Contracts;
using FinanceTracker.Ai.Mcp;

namespace FinanceTracker.Tests.Unit.Ai;

public class AiTokenScopeCheckTests : AiServiceTestBase
{
    [Test]
    public async Task AllowsAsync_UserHasAiOn_Allows()
    {
        // Arrange
        await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);

        // Act
        bool allowed = await Subject().AllowsAsync(CurrentUser.UserId, CancellationToken);

        // Assert
        await Assert.That(allowed).IsTrue();
    }

    [Test]
    public async Task AllowsAsync_UserTurnedAiOff_Refuses()
    {
        // Arrange
        await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);
        await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = false }, CancellationToken);

        // Act
        bool allowed = await Subject().AllowsAsync(CurrentUser.UserId, CancellationToken);

        // Assert
        await Assert.That(allowed).IsFalse();
    }

    [Test]
    public async Task AllowsAsync_UserNeverTurnedAiOn_Refuses()
    {
        // Act
        bool allowed = await Subject().AllowsAsync(CurrentUser.UserId, CancellationToken);

        // Assert
        await Assert.That(allowed).IsFalse();
    }

    [Test]
    public async Task AllowsAsync_AnotherUserHasAiOn_Refuses()
    {
        // Arrange
        await Service().ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);

        // Act
        bool allowed = await Subject().AllowsAsync(Guid.NewGuid(), CancellationToken);

        // Assert
        await Assert.That(allowed).IsFalse();
    }

    [Test]
    public async Task Scope_IsTheCoresNameForAiTokens()
    {
        // Assert, the core matches checks to tokens by the scope's name
        await Assert.That(Subject().Scope).IsEqualTo("Ai");
    }

    private AiTokenScopeCheck Subject() => new(ContextFactory);
}
