#if FINTRACK_AI
using FinanceTracker.Ai;
using FinanceTracker.Ai.Contracts;
using FinanceTracker.Client.Ai;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace FinanceTracker.Extensions;

/// <summary>
/// Plugs the AI module into the host. Only built with FinTrackAi=true.
/// </summary>
public static class AiModuleHostExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// The AI module's services, and what prerendering its pages needs. The router loads the module before its
        /// pages, on the server too, where the assembly is already there and loading just finds it.
        /// </summary>
        public WebApplicationBuilder AddFinTrackAiModule()
        {
            builder.AddFinTrackAi(builder.Configuration.GetFinTrackDatabaseConnectionString());
            builder.Services.AddScoped<LazyAssemblyLoader>();
            builder.Services.AddScoped<AiModuleLoader>();
            // The module's components can render on the server, e.g. on an AI page it prerenders
            builder.Services.AddScoped<AiStatusState>();
            return builder;
        }
    }
}
#endif
