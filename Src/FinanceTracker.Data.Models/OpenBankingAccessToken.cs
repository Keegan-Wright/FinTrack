namespace FinanceTracker.Data.Models;

public class OpenBankingAccessToken : BaseEntity
{
    public required Guid ProviderId { get; set; }
    public required string AccessToken { get; set; }
    public required int ExpiresIn { get; set; }
    public required string RefreshToken { get; set; }
}