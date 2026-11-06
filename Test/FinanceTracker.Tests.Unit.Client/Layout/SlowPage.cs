using FinanceTracker.Client.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FinanceTracker.Tests.Unit.Client.Layout;

/// <summary>
/// A page still loading when the user moves on: its load waits for <see cref="Gate"/>, then carries on with the page's
/// token as a page's loads do.
/// </summary>
public sealed class SlowPage : PageComponent
{
    [Parameter] [EditorRequired] public required TaskCompletionSource Gate { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Gate.Task;
        await Task.Delay(TimeSpan.FromMilliseconds(1), _cts.Token);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "Still loading");
}
