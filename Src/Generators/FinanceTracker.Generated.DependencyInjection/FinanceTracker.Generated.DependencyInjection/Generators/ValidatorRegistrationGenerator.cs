using Microsoft.CodeAnalysis;

namespace FinanceTracker.Generated.DependencyInjection;

/// <summary>
/// <c>Program.AddFinanceTrackerValidators</c>, the <c>Validator</c> category types for the project.
/// </summary>
[Generator]
public sealed class ValidatorRegistrationGenerator : RegistrationGenerator
{
    /// <inheritdoc/>
    private protected override string MethodName => "AddFinanceTrackerValidators";

    /// <inheritdoc/>
    private protected override string Category => "Validator";
}
