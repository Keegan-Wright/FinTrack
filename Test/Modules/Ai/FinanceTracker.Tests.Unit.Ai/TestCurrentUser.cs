using FinanceTracker.Infrastructure.Common;

namespace FinanceTracker.Tests.Unit.Ai;

/// <summary>
/// The signed in user, whoever a test says.
/// </summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
}
