using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Components.Pages;

public class PageComponent : ComponentBase, IDisposable
{
    internal readonly CancellationTokenSource Cts = new();

    [CascadingParameter]
    private ApplicationState ApplicationState { get; set; }

    public void Dispose()
    {
        ApplicationState.Loading = false;
        ApplicationState.LoadingMessage = null;
        Cts.Cancel();
        Cts.Dispose();
    }

    internal void SetLoadingState(bool loading, string? message = null)
    {
        ApplicationState.Loading = loading;
        ApplicationState.LoadingMessage = message;
        StateHasChanged();
    }
}
