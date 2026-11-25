using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.AppHost.Ollama;

/// <summary>
/// Runs Ollama beside FinTrack for the AI module, only in the AI build.
/// </summary>
internal static class OllamaExtensions
{
    /// <summary>
    /// The model pulled on first start when <c>Ollama:Model</c> doesn't say, small and good at calling tools.
    /// </summary>
    private const string DefaultModel = "qwen3:4b";

    /// <summary>
    /// The tokens a model reads at once when <c>Ollama:ContextLength</c> doesn't say. Ollama's own default is too
    /// small for the tools' descriptions, and cuts the start of the prompt, tools included.
    /// </summary>
    private const int DefaultContextLength = 16384;

    extension(IResourceBuilder<DotnetProjectResource> finTrack)
    {
        /// <summary>
        /// An Ollama container with its models kept in a volume, on this machine's GPU when it has one, see
        /// <see cref="OllamaGpu"/>, and FinTrack pointed at it through <c>OLLAMA_ENDPOINT</c>. With
        /// <c>Ollama:Endpoint</c> set, FinTrack uses that server instead and no container starts, e.g. Ollama installed
        /// on Windows or a Mac, where it can use the GPU the container can't. Left out when publishing, a deployment
        /// brings its own, or when <c>Ollama:Enabled</c> is false, e.g. for the tests. <c>Ollama:Port</c> fixes its
        /// port, so other tools on this machine, e.g. a chat app, can reach it between runs.
        /// <c>Ollama:ContextLength</c> sets how much a model reads at once, for every client.
        /// </summary>
        /// <param name="builder">The app's builder.</param>
        public IResourceBuilder<DotnetProjectResource> WithOllama(IDistributedApplicationBuilder builder)
        {
            if (!builder.ExecutionContext.IsRunMode || !builder.Configuration.GetValue("Ollama:Enabled", true))
            {
                return finTrack;
            }

            string? existing = builder.Configuration["Ollama:Endpoint"];
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return finTrack.WithEnvironment("OLLAMA_ENDPOINT", existing);
            }

            int? port = builder.Configuration.GetValue<int?>("Ollama:Port");
            int contextLength = builder.Configuration.GetValue("Ollama:ContextLength", DefaultContextLength);
            IResourceBuilder<OllamaResource> ollama = builder.AddOllama("ollama", port)
                .WithDataVolume()
                .WithEnvironment("OLLAMA_CONTEXT_LENGTH", contextLength.ToString(CultureInfo.InvariantCulture));
            if (OllamaGpu.Choose(builder.Configuration["Ollama:Gpu"]) is { } gpu)
            {
                ollama.WithGPUSupport(gpu);
            }

            ollama.AddModel(builder.Configuration["Ollama:Model"] ?? DefaultModel);

            // The AI module's setting, see AiService.OllamaEndpointVariable
            return finTrack.WithEnvironment("OLLAMA_ENDPOINT", ollama.GetEndpoint("http"));
        }
    }
}
