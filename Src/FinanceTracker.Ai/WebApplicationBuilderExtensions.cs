using FinanceTracker.Shared.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

namespace FinanceTracker.Ai;

public static class WebApplicationBuilderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddAiModule()
        {

            AddOllamaClients(builder);
            AddKernel(builder);

            return builder;
        }
    }

    private static void AddOllamaClients(WebApplicationBuilder builder)
    {
        builder.AddOllamaApiClient(FinanceTrackerConstants.OllamaChatServiceKey)
            .AddKeyedChatClient(FinanceTrackerConstants.OllamaChatServiceKey);
        builder.AddOllamaApiClient(FinanceTrackerConstants.OllamaEmbeddingsServiceKey)
            .AddKeyedEmbeddingGenerator(FinanceTrackerConstants.OllamaEmbeddingsServiceKey);
    }

    private static void AddKernel(WebApplicationBuilder builder)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSharedDependencies(builder.Configuration);

        var constructedConfigurationModelName = $"OLLAMA_{FinanceTrackerConstants.OllamaModelName.ToUpper().Replace(".", "_")}_URI";
        kernelBuilder.AddOllamaChatCompletion(FinanceTrackerConstants.OllamaModelName, new Uri(builder.Configuration[constructedConfigurationModelName]));
        kernelBuilder.AddOllamaEmbeddingGenerator(FinanceTrackerConstants.OllamaModelName, new Uri(builder.Configuration[constructedConfigurationModelName]));

        kernelBuilder.Plugins.AddFromType<TransactionsPlugins>();

        builder.Services.AddScoped<Kernel>(_ => kernelBuilder.Build());
    }
}
