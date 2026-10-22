using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingDirectDebitFaker : Faker<ExternalOpenBankingDirectDebit>
{
    public ExternalOpenBankingDirectDebitFaker()
    {
        RuleFor(x => x.DirectDebitId, f => $"dd-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}");
        RuleFor(x => x.Name, f => f.Company.CompanyName());
        RuleFor(x => x.Status, "Active");
        RuleFor(x => x.Currency, "GBP");
        RuleFor(x => x.PreviousPaymentTimestamp, f => f.Date.Recent(20, DateTime.UtcNow));
        RuleFor(x => x.PreviousPaymentAmount, f => f.Finance.Amount(5, 200));
        RuleFor(x => x.Timestamp, _ => DateTime.UtcNow);
    }
}
