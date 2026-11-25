using FinanceTracker.Ai.Contracts;
using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Ai.Client;

/// <summary>
/// A component that shows only once the user can use AI, the install has it set up and they've turned it on. Follows
/// them turning it on or off from any page.
/// </summary>
public abstract class AiStatusComponent : ComponentBase, IDisposable
{
    /// <summary>
    /// The user's shared AI status.
    /// </summary>
    [Inject]
    private AiStatusState AiStatusState { get; set; } = null!;

    /// <summary>
    /// Whether the user can use AI now.
    /// </summary>
    protected bool CanUseAi { get; private set; }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        AiStatusState.Changed += OnStatusChanged;
        await RefreshAsync();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        AiStatusState.Changed -= OnStatusChanged;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Shows or hides the component for the user's status. A status that can't be fetched hides it.
    /// </summary>
    private async Task RefreshAsync()
    {
        try
        {
            AiStatus status = await AiStatusState.GetAsync();
            CanUseAi = status is { Available: true, OptedIn: true };
        }
        catch (HttpRequestException)
        {
            CanUseAi = false;
        }
    }

    /// <summary>
    /// Follows the user turning AI on or off.
    /// </summary>
    private void OnStatusChanged() => _ = InvokeAsync(async () =>
    {
        await RefreshAsync();
        StateHasChanged();
    });
}
