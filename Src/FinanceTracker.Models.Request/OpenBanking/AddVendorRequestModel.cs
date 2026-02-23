using System.ComponentModel;

namespace FinanceTracker.Models.Request.OpenBanking;

public class AddVendorRequestModel
{
    [Description("Access code received from the banking provider for authentication")]
    public required string AccessCode { get; init; }
}
