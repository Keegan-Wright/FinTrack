using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingAccountConnectionFaker : Faker<ExternalOpenBankingAccountConnection>
{
    public ExternalOpenBankingAccountConnectionFaker(string? providerId = null)
    {
        RuleFor(x => x.ClientId, f => f.Random.AlphaNumeric(10));
        RuleFor(x => x.CredentialsId, f => f.Random.AlphaNumeric(10));
        RuleFor(x => x.ConsentStatus, "Authorised");
        RuleFor(x => x.ConsentCreatedAt, _ => DateTime.UtcNow);
        RuleFor(x => x.ConsentStatusUpdatedAt, _ => DateTime.UtcNow);
        RuleFor(x => x.ConsentExpiresAt, _ => DateTime.UtcNow.AddDays(90));
        RuleFor(x => x.Scopes, _ => BankConnectionFaker.Scopes.ToAsyncEnumerable());
        RuleFor(x => x.Provider, f => new ExternalOpenBankingAccountConnectionProvider
        {
            ProviderId = providerId ?? $"ob-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}",
            DisplayName = f.Company.CompanyName(),
            LogoUri = "https://logo.example.test/logo.svg"
        });
    }
}
