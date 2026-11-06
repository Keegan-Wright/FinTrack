using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FinanceTracker.Tests.Unit.Client.Layout;

/// <summary>
/// A page that fails to load while <see cref="Fail"/> says so, as one does when the server can't be reached.
/// </summary>
public sealed class FailingPage : ComponentBase
{
    public const string Loaded = "The page itself";

    [Parameter] [EditorRequired] public required Func<bool> Fail { get; set; }

    protected override void OnInitialized()
    {
        if (Fail())
        {
            throw new HttpRequestException("The server didn't answer");
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Loaded);
}
