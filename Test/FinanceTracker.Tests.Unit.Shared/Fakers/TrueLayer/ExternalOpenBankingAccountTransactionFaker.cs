using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingAccountTransactionFaker : Faker<ExternalOpenBankingAccountTransaction>
{
    public ExternalOpenBankingAccountTransactionFaker()
    {
        RuleFor(x => x.TransactionId, f => $"tx-{f.UniqueIndex}-{f.Random.AlphaNumeric(8)}");
        RuleFor(x => x.Description, f => f.Commerce.ProductName());
        RuleFor(x => x.TransactionType, f => f.PickRandom("DEBIT", "CREDIT"));
        RuleFor(x => x.TransactionCategory, f => f.PickRandom(TransactionDetailsFaker.Categories));
        RuleFor(x => x.Amount, (f, x) =>
            x.TransactionType == "DEBIT" ? -f.Finance.Amount(1, 500) : f.Finance.Amount(1, 500));
        RuleFor(x => x.Currency, "GBP");
        RuleFor(x => x.Timestamp, f => f.Date.Recent(30, DateTime.UtcNow));
        RuleFor(x => x.TransactionClassification, f =>
            f.Random.ListItems(TransactionDetailsFaker.Classifications, f.Random.Int(0, 2)).ToAsyncEnumerable());
    }
}
