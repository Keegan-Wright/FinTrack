using System.ComponentModel;

namespace FinanceTracker.Models.Request.Classifications;

public class AddClassificationsRequest
{
    [Description("Tag or label for the new classification")]
    public required string Tag { get; set; }
}