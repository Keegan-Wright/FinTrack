using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// A custom classification's new name.
/// </summary>
public class RenameClassification
{
    /// <summary>
    /// The classification's new name.
    /// </summary>
    [Description("New tag or label for the classification")]
    public required string Tag { get; set; }
}
