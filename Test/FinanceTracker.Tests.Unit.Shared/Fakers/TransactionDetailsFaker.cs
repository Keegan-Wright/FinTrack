using Bogus;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

/// <summary>
/// Provider transaction details, amounts are signed to match the transaction type.
/// Never generates <c>TRANSFER</c>, which most queries exclude.
/// </summary>
public sealed class TransactionDetailsFaker : Faker<TransactionDetails>
{
    public static readonly string[] Categories = ["PURCHASE", "BILL_PAYMENT", "DIRECT_DEBIT", "ATM", "CREDIT"];
    public static readonly string[] Classifications = ["Shopping", "Groceries", "Entertainment", "Bills", "Travel"];

    public TransactionDetailsFaker() =>
        CustomInstantiator(f =>
        {
            TransactionType type = f.PickRandom<TransactionType>();
            decimal amount = f.Finance.Amount(1, 500);

            return new TransactionDetails(
                $"tx-{f.UniqueIndex}-{f.Random.AlphaNumeric(8)}",
                f.Commerce.ProductName(),
                type,
                f.PickRandom(Categories),
                type == TransactionType.Debit ? -amount : amount,
                "GBP",
                f.Date.Recent(30, DateTime.UtcNow),
                false,
                f.Random.ListItems(Classifications, f.Random.Int(0, 2)).ToList());
        });
}
