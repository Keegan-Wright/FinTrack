using System.Text.Json;
using FinanceTracker.Ai.Tools;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.Ai;

/// <summary>
/// Calls tools the way a model does, by name with JSON arguments, against substitute services.
/// </summary>
public abstract class AiToolTestBase
{
    protected static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    protected AiTools Tools { get; } = new();

    protected ServiceCollection Services { get; } = new();

    protected AiToolTestBase()
    {
        // Days start at midnight UTC, so date filters are easy to read
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now, TimeZoneInfo.Utc));
        // No public address, so links are paths
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton<PageLinks>();
    }

    protected AIFunction Tool(string name) => Tools.Functions.Single(x => x.Name == name);

    protected async Task<JsonElement> CallAsync(string name, object? arguments = null)
    {
        await using ServiceProvider provider = Services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        Dictionary<string, object?> values = [];
        if (arguments is not null)
        {
            JsonElement json = JsonSerializer.SerializeToElement(arguments, AiTools.SerializerOptions);
            foreach (JsonProperty property in json.EnumerateObject())
            {
                values[property.Name] = property.Value;
            }
        }

        AIFunctionArguments functionArguments = new(values) { Services = scope.ServiceProvider };
        object? result = await Tool(name).InvokeAsync(functionArguments);
        return JsonSerializer.SerializeToElement(result, AiTools.SerializerOptions);
    }
}
