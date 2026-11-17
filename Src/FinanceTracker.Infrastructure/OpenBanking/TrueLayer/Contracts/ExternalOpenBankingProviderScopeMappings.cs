using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// Which of the bank's scopes give each of TrueLayer's.
/// </summary>
public class ExternalOpenBankingProviderScopeMappings
{
    /// <summary>
    /// The bank's scopes for the user's details.
    /// </summary>
    public IAsyncEnumerable<string>? Info { get; set; }

    /// <summary>
    /// The bank's scopes for accounts.
    /// </summary>
    public IAsyncEnumerable<string>? Accounts { get; set; }

    /// <summary>
    /// The bank's scopes for balances.
    /// </summary>
    public IAsyncEnumerable<string>? Balance { get; set; }

    /// <summary>
    /// The bank's scopes for transactions.
    /// </summary>
    public IAsyncEnumerable<string>? Transactions { get; set; }

    /// <summary>
    /// The bank's scopes for cards.
    /// </summary>
    public IAsyncEnumerable<string>? Cards { get; set; }

    /// <summary>
    /// The bank's scopes for syncing while the user's away.
    /// </summary>
    [JsonPropertyName("offline_access")]
    public IAsyncEnumerable<string>? OfflineAccess { get; set; }

    /// <summary>
    /// The bank's scopes for direct debits.
    /// </summary>
    [JsonPropertyName("direct_debits")]
    public IAsyncEnumerable<string>? DirectDebits { get; set; }

    /// <summary>
    /// The bank's scopes for standing orders.
    /// </summary>
    [JsonPropertyName("standing_orders")]
    public IAsyncEnumerable<string>? StandingOrders { get; set; }

    /// <summary>
    /// The bank's scopes for saved payees.
    /// </summary>
    public IAsyncEnumerable<string>? Beneficiaries { get; set; }

    /// <summary>
    /// The bank's scopes for scheduled payments.
    /// </summary>
    [JsonPropertyName("scheduled_payments")]
    public IAsyncEnumerable<string>? ScheduledPayments { get; set; }

    /// <summary>
    /// The bank's scopes for the user's name.
    /// </summary>
    [JsonPropertyName("info_name")]
    public IAsyncEnumerable<string>? InfoName { get; set; }

    /// <summary>
    /// The bank's scopes for the user's date of birth.
    /// </summary>
    [JsonPropertyName("info_date_of_birth")]
    public IAsyncEnumerable<string>? InfoDateOfBirth { get; set; }

    /// <summary>
    /// The bank's scopes for the user's addresses.
    /// </summary>
    [JsonPropertyName("info_addresses")]
    public IAsyncEnumerable<string>? InfoAddresses { get; set; }

    /// <summary>
    /// The bank's scopes for the user's phone numbers.
    /// </summary>
    [JsonPropertyName("info_phone_numbers")]
    public IAsyncEnumerable<string>? InfoPhoneNumbers { get; set; }

    /// <summary>
    /// The bank's scopes for the user's email addresses.
    /// </summary>
    [JsonPropertyName("info_email_addresses")]
    public IAsyncEnumerable<string>? InfoEmailAddresses { get; set; }
}
