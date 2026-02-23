using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProviderScopeMappings
{
    public IAsyncEnumerable<string>? Info { get; set; }
    public IAsyncEnumerable<string>? Accounts { get; set; }
    public IAsyncEnumerable<string>? Balance { get; set; }
    public IAsyncEnumerable<string>? Transactions { get; set; }
    public IAsyncEnumerable<string>? Cards { get; set; }

    [JsonPropertyName("offline_access")]
    public IAsyncEnumerable<string>? OfflineAccess { get; set; }

    [JsonPropertyName("direct_debits")]
    public IAsyncEnumerable<string>? DirectDebits { get; set; }

    [JsonPropertyName("standing_orders")]
    public IAsyncEnumerable<string>? StandingOrders { get; set; }

    public IAsyncEnumerable<string>? Beneficiaries { get; set; }

    [JsonPropertyName("scheduled_payments")]
    public IAsyncEnumerable<string>? ScheduledPayments { get; set; }

    [JsonPropertyName("info_name")]
    public IAsyncEnumerable<string>? InfoName { get; set; }

    [JsonPropertyName("info_date_of_birth")]
    public IAsyncEnumerable<string>? InfoDateOfBirth { get; set; }

    [JsonPropertyName("info_addresses")]
    public IAsyncEnumerable<string>? InfoAddresses { get; set; }

    [JsonPropertyName("info_phone_numbers")]
    public IAsyncEnumerable<string>? InfoPhoneNumbers { get; set; }

    [JsonPropertyName("info_email_addresses")]
    public IAsyncEnumerable<string>? InfoEmailAddresses { get; set; }
}
