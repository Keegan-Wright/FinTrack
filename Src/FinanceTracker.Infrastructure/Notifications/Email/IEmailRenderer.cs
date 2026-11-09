using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// Renders an email's HTML from a Razor component, see <c>Templates</c>, so every value in it is escaped the way a
/// page's is.
/// </summary>
public interface IEmailRenderer
{
    /// <returns>The component's HTML.</returns>
    Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters) where TComponent : IComponent;
}
