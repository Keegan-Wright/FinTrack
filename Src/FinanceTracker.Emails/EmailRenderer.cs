using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Emails;

/// <summary>
/// <see cref="IEmailRenderer"/> with Blazor's <see cref="HtmlRenderer"/>, a renderer per email as each lives only
/// as long as its render. Each render gets its own scope, as the renderer needs scoped services the root provider
/// won't hand out.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IEmailRenderer>]
public sealed class EmailRenderer : IEmailRenderer
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <param name="serviceScopeFactory">Makes the scope each render gets.</param>
    /// <param name="loggerFactory">The renderer's loggers.</param>
    public EmailRenderer(IServiceScopeFactory serviceScopeFactory, ILoggerFactory loggerFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public async Task<string> RenderAsync<TComponent>(IDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        await using HtmlRenderer renderer = new(scope.ServiceProvider, _loggerFactory);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent root =
                await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return root.ToHtmlString();
        });
    }
}
