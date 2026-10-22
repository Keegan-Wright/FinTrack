using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingAccessResponseFaker : Faker<ExternalOpenBankingAccessResponse>
{
    public ExternalOpenBankingAccessResponseFaker()
    {
        RuleFor(x => x.AccessToken, f => f.Random.AlphaNumeric(40));
        RuleFor(x => x.RefreshToken, f => f.Random.AlphaNumeric(40));
        RuleFor(x => x.ExpiresIn, 3600);
        RuleFor(x => x.TokenType, "Bearer");
    }
}
