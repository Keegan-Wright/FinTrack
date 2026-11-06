using TUnit.Core.Interfaces;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// How many UI tests run at once. Each starts the app's WebAssembly in a browser of its own, all from the one app, so
/// too many at once leave every page waiting to start rather than finishing sooner. Half the cores, from 2 to 8.
/// </summary>
public sealed class UiParallelLimit : IParallelLimit
{
    public int Limit { get; } = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
}
