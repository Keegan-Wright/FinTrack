using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingCardFaker : Faker<ExternalOpenBankingCard>
{
    public ExternalOpenBankingCardFaker()
    {
        RuleFor(x => x.AccountId, f => $"card-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}");
        RuleFor(x => x.CardNetwork, f => f.PickRandom("VISA", "MASTERCARD"));
        RuleFor(x => x.CardType, "CREDIT");
        RuleFor(x => x.Currency, "GBP");
        RuleFor(x => x.DisplayName, f => $"{f.Company.CompanyName()} Card");
        RuleFor(x => x.PartialCardNumber, f => f.Random.ReplaceNumbers("####"));
        RuleFor(x => x.NameOnCard, "A N Other");
        RuleFor(x => x.ValidTo, _ => DateTime.UtcNow.AddYears(3));
        RuleFor(x => x.UpdateTimestamp, _ => DateTime.UtcNow);
    }
}
