using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer;

/// <summary>
/// Translates TrueLayer payloads into domain details and contracts. Timestamps are normalised to UTC.
/// </summary>
internal static class TrueLayerMappings
{
    extension(ExternalOpenBankingAccountStandingOrder standingOrder)
    {
        public StandingOrderDetails ToDetails() => new(
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
    }

    extension(ExternalOpenBankingDirectDebit directDebit)
    {
        public DirectDebitDetails ToDetails() => new(
            directDebit.DirectDebitId,
            directDebit.Name,
            directDebit.Status,
            directDebit.PreviousPaymentTimestamp.ToUtc(),
            directDebit.PreviousPaymentAmount,
            directDebit.Currency,
            directDebit.Timestamp.ToUtc());
    }

    extension(ExternalOpenBankingAccountTransaction transaction)
    {
        public async Task<TransactionDetails> ToDetailsAsync(bool isPending, CancellationToken cancellationToken)
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
                classifications,
                new TransactionMetadata(
                    NullIfBlank(transaction.Meta?.ProviderReference),
                    NullIfBlank(transaction.Meta?.ProviderCategory),
                    NullIfBlank(transaction.MerchantName),
                    NullIfBlank(transaction.Meta?.CounterPartyPreferredName),
                    NullIfBlank(transaction.Meta?.DebtorAccountName)));
        }
    }

    // Banks leave out the fields they don't fill, which deserialise as empty
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    extension(ExternalOpenBankingProvider provider)
    {
        /// <summary>
        /// The bank as pages see it, so TrueLayer's payload stays on the server.
        /// </summary>
        public async Task<OpenBankingProvider> ToContractAsync(CancellationToken cancellationToken) => new()
        {
            ProviderId = provider.ProviderId,
            DisplayName = provider.DisplayName,
            Country = provider.Country,
            LogoUrl = provider.LogoUrl,
            Scopes = provider.Scopes is null ? [] : await provider.Scopes.ToListAsync(cancellationToken)
        };
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

    extension(DateTime value)
    {
        internal DateTime ToUtc() => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
