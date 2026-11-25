using Microsoft.Extensions.AI;

namespace FinanceTracker.Ai.Tools;

/// <summary>
/// Every tool a model can call, the same set for the built-in chat and for MCP clients. Each reads the signed-in
/// user's data through the services, and none changes anything.
/// </summary>
public interface IAiTools
{
    /// <summary>
    /// The tools' methods, for MCP, which makes its own tools from them.
    /// </summary>
    IReadOnlyList<AiToolMethod> Methods { get; }

    /// <summary>
    /// The tools. Calling one needs the request's services in the call's
    /// <see cref="AIFunctionArguments.Services"/>, so it runs as the signed-in user.
    /// </summary>
    IReadOnlyList<AIFunction> Functions { get; }
}
