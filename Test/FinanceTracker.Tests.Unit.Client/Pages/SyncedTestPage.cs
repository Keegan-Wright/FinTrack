using FinanceTracker.Client.Pages;
using Microsoft.AspNetCore.Components.Rendering;

namespace FinanceTracker.Tests.Unit.Client.Pages;

/// <summary>
/// A page that counts its refreshes after a sync, and does what <see cref="Refresh"/> says each time.
/// </summary>
public sealed class SyncedTestPage : PageComponent
{
    public int Refreshes { get; private set; }

    /// <summary>
    /// What each refresh does, e.g. waits or fails.
    /// </summary>
    public Func<Task> Refresh { get; set; } = static () => Task.CompletedTask;

    protected override async Task OnSyncedAsync()
    {
        Refreshes++;
        await Refresh();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, $"Refreshed {Refreshes}");
}
