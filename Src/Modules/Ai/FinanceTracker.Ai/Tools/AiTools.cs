using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Ai.Tools;

/// <inheritdoc cref="IAiTools"/>
/// <remarks>
/// Built once from every method marked <see cref="AiToolAttribute"/> in this assembly. Each call makes a new
/// instance of the tool's class from the call's services, so the services it uses are the request's, scoped to the
/// signed in user.
/// </remarks>
public sealed class AiTools : IAiTools
{
    /// <summary>
    /// How tool arguments and results are read and written. Enums go as their names, which models handle far better
    /// than numbers.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    /// <inheritdoc/>
    public IReadOnlyList<AiToolMethod> Methods { get; } = FindMethods();

    /// <inheritdoc/>
    public IReadOnlyList<AIFunction> Functions { get; }

    /// <summary>
    /// The tools, found once.
    /// </summary>
    public AiTools() => Functions = Methods.Select(CreateFunction).ToList();

    /// <summary>
    /// Makes the class a tool's method is called on, from the call's services, so it runs as the signed in user.
    /// </summary>
    /// <param name="type">The tool class.</param>
    /// <param name="services">The call's services.</param>
    /// <exception cref="InvalidOperationException">The call has no services, so it can't know who the user
    /// is.</exception>
    public static object CreateTarget(Type type, IServiceProvider? services)
    {
        if (services is null)
        {
            throw new InvalidOperationException(
                $"Tool calls need the request's services, so {type.Name} runs as the signed-in user.");
        }

        return ActivatorUtilities.CreateInstance(services, type);
    }

    /// <summary>
    /// Every tool method in this assembly.
    /// </summary>
    private static List<AiToolMethod> FindMethods()
    {
        List<AiToolMethod> methods = [];
        foreach (Type type in typeof(AiTools).Assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<AiToolAttribute>() is { } tool)
                {
                    methods.Add(new AiToolMethod(tool.Name, type, method));
                }
            }
        }

        return methods;
    }

    /// <summary>
    /// The function a model calls for <paramref name="tool"/>.
    /// </summary>
    /// <param name="tool">The tool.</param>
    private static AIFunction CreateFunction(AiToolMethod tool)
    {
        AIFunctionFactoryOptions options = new()
        {
            Name = tool.Name,
            SerializerOptions = SerializerOptions
        };
        return AIFunctionFactory.Create(tool.Method, arguments => CreateTarget(tool.Type, arguments.Services),
            options);
    }

    /// <summary>
    /// The library's defaults, with enums as their names.
    /// </summary>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new(AIJsonUtilities.DefaultOptions);
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly();
        return options;
    }
}
