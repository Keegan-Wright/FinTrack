namespace FinanceTracker.Ai.Tools;

/// <summary>
/// Marks a method as a tool a model can call. Its <see cref="System.ComponentModel.DescriptionAttribute"/> and its
/// parameters' are what the model reads, see <see cref="AiTools"/>.
/// </summary>
/// <param name="name">The tool's name as the model sees it, in snake case.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AiToolAttribute(string name) : Attribute
{
    /// <summary>
    /// The tool's name as the model sees it, in snake case.
    /// </summary>
    public string Name { get; } = name;
}
