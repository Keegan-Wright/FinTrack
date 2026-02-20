using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingAccessToken : BaseEntity
{
    public required Guid ProviderId { get; set; }
    
    [Encrypt]
    public required string AccessToken { get; set; }
    
    [Encrypt]
    public required int ExpiresIn { get; set; }
    
    [Encrypt]
    public required string RefreshToken { get; set; }
}