namespace FinanceTracker.Tests.Shared;

public class TestFixtureBase
{
    protected internal CancellationTokenSource _cancellationTokenSource;

    public TestFixtureBase()
    {
        _cancellationTokenSource = new CancellationTokenSource();
    }
}
