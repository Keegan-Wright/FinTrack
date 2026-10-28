using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.OpenBanking;

/// <summary>
/// Where a bank connection's consent is in its lifetime.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConsentState>))]
public enum ConsentState
{
    /// <summary>
    /// Syncing, with time to spare.
    /// </summary>
    Active,

    /// <summary>
    /// Still syncing, but consent ends soon and should be renewed.
    /// </summary>
    RenewSoon,

    /// <summary>
    /// Consent has ended, nothing syncs until it's renewed.
    /// </summary>
    Lapsed
}
