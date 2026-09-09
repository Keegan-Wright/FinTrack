using System.ComponentModel;

namespace FinanceTracker.Contracts.OpenBanking;

public class AddVendor
{
    [Description("Access code received from the banking provider for authentication")]
    public required string AccessCode { get; init; }
}
