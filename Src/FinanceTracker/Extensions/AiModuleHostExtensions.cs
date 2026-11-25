#if FINTRACK_AI
using FinanceTracker.Ai;
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
            return builder;
        }
    }
}
#endif
