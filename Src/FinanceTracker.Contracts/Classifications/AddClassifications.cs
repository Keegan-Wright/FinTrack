using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

public class AddClassifications
{
    [Description("Tag or label for the new classification")]
    public required string Tag { get; set; }
}
