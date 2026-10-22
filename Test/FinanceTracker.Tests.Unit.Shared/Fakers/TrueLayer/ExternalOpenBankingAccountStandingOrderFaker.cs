using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingAccountStandingOrderFaker : Faker<ExternalOpenBankingAccountStandingOrder>
{
    public ExternalOpenBankingAccountStandingOrderFaker()
    {
        RuleFor(x => x.Reference, f => $"REF-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}");
        RuleFor(x => x.Payee, f => f.Name.FullName());
        RuleFor(x => x.Frequency, "Monthly");
        RuleFor(x => x.Status, "Active");
        RuleFor(x => x.Currency, "GBP");
        RuleFor(x => x.NextPaymentDate, f => f.Date.Soon(28, DateTime.UtcNow));
        RuleFor(x => x.NextPaymentAmount, f => f.Random.Int(5, 500));
        RuleFor(x => x.FirstPaymentDate, f => f.Date.Past(1, DateTime.UtcNow));
        RuleFor(x => x.FirstPaymentAmount, (_, x) => x.NextPaymentAmount);
        RuleFor(x => x.FinalPaymentDate, f => f.Date.Future(2, DateTime.UtcNow));
        RuleFor(x => x.FinalPaymentAmount, (_, x) => x.NextPaymentAmount);
        RuleFor(x => x.Timestamp, _ => DateTime.UtcNow);
    }
}
