using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer;

/// <summary>
/// Translates TrueLayer payloads into domain details. Timestamps are normalised to UTC.
/// </summary>
internal static class TrueLayerMappings
{
    public static StandingOrderDetails ToDetails(this ExternalOpenBankingAccountStandingOrder standingOrder) => new(
        standingOrder.Reference,
        standingOrder.Payee,
        standingOrder.Frequency,
        standingOrder.Status,
        standingOrder.Currency,
        standingOrder.NextPaymentDate.ToUtc(),
        standingOrder.NextPaymentAmount,
        standingOrder.FirstPaymentDate.ToUtc(),
        standingOrder.FirstPaymentAmount,
        standingOrder.FinalPaymentDate.ToUtc(),
        standingOrder.FinalPaymentAmount,
        standingOrder.Timestamp.ToUtc());

    public static DirectDebitDetails ToDetails(this ExternalOpenBankingDirectDebit directDebit) => new(
        directDebit.DirectDebitId,
        directDebit.Name,
        directDebit.Status,
        directDebit.PreviousPaymentTimestamp.ToUtc(),
        directDebit.PreviousPaymentAmount,
        directDebit.Currency,
        directDebit.Timestamp.ToUtc());

    public static async Task<TransactionDetails> ToDetailsAsync(this ExternalOpenBankingAccountTransaction transaction,
        bool isPending, CancellationToken cancellationToken)
    {
        List<string> classifications = transaction.TransactionClassification is null
            ? []
            : await transaction.TransactionClassification.ToListAsync(cancellationToken);

        return new TransactionDetails(
            transaction.TransactionId,
            transaction.Description,
            ParseTransactionType(transaction),
            transaction.TransactionCategory,
            transaction.Amount,
            transaction.Currency,
            transaction.Timestamp.ToUtc(),
            isPending,
            classifications);
    }

    /// <summary>
    /// TrueLayer reports <c>DEBIT</c> / <c>CREDIT</c>
    /// </summary>
    private static TransactionType ParseTransactionType(ExternalOpenBankingAccountTransaction transaction) =>
        Enum.TryParse(transaction.TransactionType, true, out TransactionType type)
            ? type
            : decimal.IsNegative(transaction.Amount)
                ? TransactionType.Debit
                : TransactionType.Credit;

    internal static DateTime ToUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
