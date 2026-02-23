using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Components.Pages;

public class PageComponent : ComponentBase, IDisposable
{
    internal readonly CancellationTokenSource _cts = new();

    [CascadingParameter]
    private ApplicationState ApplicationState { get; set; } = null!;

    public void Dispose()
    {
        ApplicationState.Loading = false;
        ApplicationState.LoadingMessage = null;
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    internal void SetLoadingState(bool loading, string? message = null)
    {
        ApplicationState.Loading = loading;
        ApplicationState.LoadingMessage = message;
        StateHasChanged();
    }
}
