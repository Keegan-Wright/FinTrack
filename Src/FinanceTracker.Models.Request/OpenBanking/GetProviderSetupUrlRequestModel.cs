using System.Collections.Immutable;
using System.ComponentModel;

namespace FinanceTracker.Models.Request.OpenBanking;

public class GetProviderSetupUrlRequestModel
{
    [Description("Collection of banking provider identifiers to set up")]
    public required IImmutableList<string> ProviderIds { get; init; }

    [Description("Collection of access scopes required for the banking integration")]
    public required IImmutableList<string> Scopes { get; init; }
}
