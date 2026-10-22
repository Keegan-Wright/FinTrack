using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// A custom classification picked to apply to a transaction.
/// </summary>
public class SelectedCustomClassification
{
    /// <summary>
    /// The classification, see <see cref="Classification.ClassificationId"/>.
    /// </summary>
    [Description("Unique identifier of the classification to apply")]
    public required Guid ClassificationId { get; init; }
}
