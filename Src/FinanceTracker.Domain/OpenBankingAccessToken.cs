using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingAccessToken : BaseEntity
{
    public OpenBankingAccessToken(Guid providerId, string accessToken, int expiresIn, string refreshToken, bool expired)
    {
        ProviderId = providerId;
        AccessToken = accessToken;
        ExpiresIn = expiresIn;
        RefreshToken = refreshToken;
        Expired = expired;
    }

    public Guid ProviderId { get; private set; }

    [Encrypt] public string AccessToken { get; private set; }

    [Encrypt] public int ExpiresIn { get; private set; }

    [Encrypt] public string RefreshToken { get; private set; }

    public bool Expired { get; private set; }

    public void Expire() => Expired = true;
}
