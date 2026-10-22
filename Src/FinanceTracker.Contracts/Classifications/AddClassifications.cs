using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// A new custom classification. Validated by <see cref="Validation.AddClassificationsRequestValidator"/>.
/// </summary>
public class AddClassifications
{
    /// <summary>
    /// The classification's name
    /// </summary>
    [Description("Tag or label for the new classification")]
    public required string Tag { get; set; }
}
