namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence.EncryptedContext;

/// <summary>
/// A row in <see cref="TestEncryptedContext"/>, with one encrypted column.
/// </summary>
public sealed class TestNote
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Text { get; set; }
}
