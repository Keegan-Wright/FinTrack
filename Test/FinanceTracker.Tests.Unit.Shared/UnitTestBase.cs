namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// Base for unit tests that don't need a database, provides a per test cancellation token.
/// </summary>
public abstract class UnitTestBase
{
    private CancellationTokenSource CancellationTokenSource { get; set; } = null!;
    protected CancellationToken CancellationToken => CancellationTokenSource.Token;

    [Before(Test)]
    public void BaseUnitSetup() => CancellationTokenSource = new CancellationTokenSource();

    [After(Test)]
    public async Task BaseUnitCleanupAsync()
    {
        await CancellationTokenSource.CancelAsync();
        CancellationTokenSource.Dispose();
    }
}
