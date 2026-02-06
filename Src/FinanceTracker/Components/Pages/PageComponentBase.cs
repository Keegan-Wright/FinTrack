using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Components.Pages;

public class PageComponent : ComponentBase, IDisposable
{
    internal readonly CancellationTokenSource Cts = new();
    
    internal bool Loading {get; private set; }
    internal string? LoadingMessage { get; private set; }

    internal void SetLoadingState(bool loading, string? message)
    {
        Loading = loading;
        LoadingMessage = message;
    }
    
    public void Dispose()
    {
        Cts.Cancel();
        Cts.Dispose();
    }
}