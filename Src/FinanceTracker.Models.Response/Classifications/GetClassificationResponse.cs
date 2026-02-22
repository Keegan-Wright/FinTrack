using System.ComponentModel;

namespace FinanceTracker.Models.Response.Classifications;

public class GetClassificationResponse
{
    [Description("Unique identifier for the classification")]
    public Guid ClassificationId { get; set; }

    [Description("Tag or label associated with the classification")]
    public string Tag { get; set; }
}
