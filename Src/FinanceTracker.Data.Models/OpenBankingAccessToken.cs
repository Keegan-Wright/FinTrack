using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingAccessToken : BaseEntity
{
    public required Guid ProviderId { get; init; }

    [Encrypt]
    public required string AccessToken { get; init; }

    [Encrypt]
    public required int ExpiresIn { get; init; }

    [Encrypt]
    public required string RefreshToken { get; init; }
}
