using System.Reflection;

namespace FinanceTracker.Ai.Tools;

/// <summary>
/// One tool, the method a model calls and the class it's on, from which both MCP's tool and the chat's function are
/// made.
/// </summary>
/// <param name="Name">The tool's name as a model sees it.</param>
/// <param name="Type">The class the method is on, made per call.</param>
/// <param name="Method">The method.</param>
public sealed record AiToolMethod(string Name, Type Type, MethodInfo Method);
