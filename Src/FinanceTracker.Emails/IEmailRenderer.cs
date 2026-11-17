using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Emails;

/// <summary>
/// Renders an email's HTML from a Razor component, see <c>Templates</c>, so every value in it is escaped the way a
/// page's is.
/// </summary>
public interface IEmailRenderer
{
    /// <summary>
    /// Renders <typeparamref name="TComponent"/> to HTML.
    /// </summary>
    /// <typeparam name="TComponent">The email's template.</typeparam>
    /// <param name="parameters">The template's parameters, by name.</param>
    /// <returns>The component's HTML.</returns>
    Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters) where TComponent : IComponent;
}
