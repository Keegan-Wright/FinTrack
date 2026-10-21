using System.Security.Claims;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class CurrentUserTests : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();

    [Test]
    public async Task UserId_WhenNothingIsSet_Throws()
    {
        // Arrange
        CurrentUser subject = new();

        // Act & Assert
        await Assert.That(() => subject.UserId).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Set_SignedInPrincipal_UsesItsNameIdentifier()
    {
        // Arrange
        CurrentUser subject = new();
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _userId.ToString())], "test"));

        // Act
        subject.Set(principal);

        // Assert
        await Assert.That(subject.UserId).IsEqualTo(_userId);
    }

    [Test]
    public async Task Set_AnonymousPrincipal_ClearsTheUser()
    {
        // Arrange
        CurrentUser subject = new();
        subject.Set(_userId);

        // Act
        subject.Set(new ClaimsPrincipal(new ClaimsIdentity()));

        // Assert
        await Assert.That(() => subject.UserId).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Set_NullPrincipal_ClearsTheUser()
    {
        // Arrange
        CurrentUser subject = new();
        subject.Set(_userId);

        // Act
        subject.Set(principal: null);

        // Assert
        await Assert.That(() => subject.UserId).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Set_EmptyUserId_Throws()
    {
        // Arrange
        CurrentUser subject = new();

        // Act & Assert
        await Assert.That(() => subject.Set(Guid.Empty)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CreateUserScope_ResolvesTheUserThroughICurrentUser()
    {
        // Arrange
        await using ServiceProvider provider = new ServiceCollection().AddCurrentUser().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Act
        await using AsyncServiceScope scope = scopeFactory.CreateUserScope(_userId);

        // Assert
        await Assert.That(scope.ServiceProvider.GetRequiredService<ICurrentUser>().UserId).IsEqualTo(_userId);
    }

    [Test]
    public async Task CreateUserScope_ScopesDoNotShareTheirUser()
    {
        // Arrange
        Guid otherUserId = Guid.NewGuid();
        await using ServiceProvider provider = new ServiceCollection().AddCurrentUser().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Act
        await using AsyncServiceScope first = scopeFactory.CreateUserScope(_userId);
        await using AsyncServiceScope second = scopeFactory.CreateUserScope(otherUserId);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.ServiceProvider.GetRequiredService<ICurrentUser>().UserId).IsEqualTo(_userId);
            await Assert.That(second.ServiceProvider.GetRequiredService<ICurrentUser>().UserId).IsEqualTo(otherUserId);
        }
    }
}
