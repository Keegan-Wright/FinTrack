using System.Linq;
using Microsoft.CodeAnalysis;

namespace FinanceTracker.Generated.DependencyInjection;

/// <summary>
/// Adds a method to the project's <c>Program</c> registering the <see cref="Category"/> types for the project's
/// destination.
/// </summary>
public abstract class RegistrationGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The generated <c>Program</c> method, e.g. <c>AddFinanceTrackerServices</c>.
    /// </summary>
    private protected abstract string MethodName { get; }

    /// <summary>
    /// The <c>InjectionCategoryType</c> member the method registers.
    /// </summary>
    private protected abstract string Category { get; }

    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider),
            (spc, source) =>
            {
                if (RegistrationProgram.Find(source.Left, source.Right) is not { } program)
                {
                    return;
                }

                spc.AddSource($"Program.{MethodName}.g.cs", program.Write(MethodName,
                    InjectableTypeCollector.Collect(source.Left).Where(x =>
                        x.Category == Category && x.Destinations.Contains(program.Destination))));
            });
}
