using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class AuditingInterceptorTests : InfrastructureTestBase
{
    [Test]
    public async Task SavingChanges_WithoutContext_PassesResultThrough()
    {
        // Arrange
        ISaveChangesInterceptor subject = await GetInterceptorAsync();
        DbContextEventData eventData = new(new TestEventDefinition(), static (_, _) => string.Empty, null);
        InterceptionResult<int> result = InterceptionResult<int>.SuppressWithResult(3);

        // Act
        InterceptionResult<int> syncResult = subject.SavingChanges(eventData, result);
        InterceptionResult<int> asyncResult = await subject.SavingChangesAsync(eventData, result, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(syncResult.Result).IsEqualTo(3);
            await Assert.That(asyncResult.Result).IsEqualTo(3);
        }
    }

    /// <summary>
    /// The interceptor is internal, it is taken from the options the context configures itself with.
    /// </summary>
    private async Task<ISaveChangesInterceptor> GetInterceptorAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors!
            .OfType<ISaveChangesInterceptor>()
            .Single();
    }

    private sealed class TestEventDefinition() : EventDefinitionBase(Substitute.For<ILoggingOptions>(),
        new EventId(1), LogLevel.Debug, "Test");
}
