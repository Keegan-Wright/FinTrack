using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// <see cref="IEmailRenderer"/> with Blazor's <see cref="HtmlRenderer"/>, a renderer per email as each lives only
/// as long as its render.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IEmailRenderer>]
public sealed class EmailRenderer : IEmailRenderer
{
    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;

    public EmailRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
    {
        _services = services;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using HtmlRenderer renderer = new(_services, _loggerFactory);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent root =
                await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return root.ToHtmlString();
        });
    }
}
