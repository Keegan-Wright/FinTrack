using System.ComponentModel;

namespace FinanceTracker.Contracts.OpenBanking;

/// <summary>
/// Finishes connecting banks after the user consents on the provider's page.
/// </summary>
public class AddVendor
{
    /// <summary>
    /// The code the provider's consent page hands back.
    /// </summary>
    [Description("Access code received from the banking provider for authentication")]
    public required string AccessCode { get; init; }
}
