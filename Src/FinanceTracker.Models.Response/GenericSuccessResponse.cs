using System.ComponentModel;

namespace FinanceTracker.Models.Response;

public class GenericSuccessResponse
{
    [Description("Indicates if the operation was successful")]
    public bool Success { get; set; }
}
