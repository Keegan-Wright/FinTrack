using System.Text.Json;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.OpenBanking;

/// <summary>
/// TrueLayer payloads deserialise onto the contracts, with the same options the HTTP client reads them with.
/// </summary>
public class TrueLayerContractTests : UnitTestBase
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task Account_DeserialisesAccountNumberAndProvider()
    {
        // Arrange
        const string json = """
            {
              "account_id": "acc-1",
              "account_number": { "iban": "GB00TEST", "swift_bic": "TESTGB2L", "number": "12345678", "sort_code": "01-02-03" },
              "provider": { "display_name": "Test Bank", "provider_id": "ob-test", "logo_uri": "https://logo.test/bank.svg" }
            }
            """;

        // Act
        ExternalOpenBankingAccount account = JsonSerializer.Deserialize<ExternalOpenBankingAccount>(json, WebOptions)!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(account.AccountNumber!.Iban).IsEqualTo("GB00TEST");
            await Assert.That(account.AccountNumber.SwiftBic).IsEqualTo("TESTGB2L");
            await Assert.That(account.AccountNumber.Number).IsEqualTo("12345678");
            await Assert.That(account.AccountNumber.SortCode).IsEqualTo("01-02-03");
            await Assert.That(account.Provider!.DisplayName).IsEqualTo("Test Bank");
            await Assert.That(account.Provider.ProviderId).IsEqualTo("ob-test");
            await Assert.That(account.Provider.LogoUri).IsEqualTo("https://logo.test/bank.svg");
        }
    }

    [Test]
    public async Task Transaction_DeserialisesMetadata()
    {
        // Arrange
        const string json = """
            {
              "transaction_id": "tx-1",
              "merchant_name": "Coffee Co",
              "provider_transaction_id": "ptx-1",
              "normalised_provider_transaction_id": "ntx-1",
              "meta": {
                "provider_category": "PURCHASE",
                "transaction_type": "DEBIT",
                "provider_id": "ob-test",
                "counter_party_preferred_name": "Coffee Co Ltd",
                "debtor_account_name": "Jane Doe"
              }
            }
            """;

        // Act
        ExternalOpenBankingAccountTransaction transaction =
            JsonSerializer.Deserialize<ExternalOpenBankingAccountTransaction>(json, WebOptions)!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.MerchantName).IsEqualTo("Coffee Co");
            await Assert.That(transaction.ProviderTransactionId).IsEqualTo("ptx-1");
            await Assert.That(transaction.NormalisedProviderTransactionId).IsEqualTo("ntx-1");
            await Assert.That(transaction.Meta!.ProviderCategory).IsEqualTo("PURCHASE");
            await Assert.That(transaction.Meta.TransactionType).IsEqualTo("DEBIT");
            await Assert.That(transaction.Meta.ProviderId).IsEqualTo("ob-test");
            await Assert.That(transaction.Meta.CounterPartyPreferredName).IsEqualTo("Coffee Co Ltd");
            await Assert.That(transaction.Meta.DebtorAccountName).IsEqualTo("Jane Doe");
        }
    }

    [Test]
    public async Task DirectDebit_DeserialisesMetadata()
    {
        // Arrange
        const string json = """
            { "direct_debit_id": "dd-1", "meta": { "provider_mandate_identification": "mandate-1", "provider_account_id": "pacc-1" } }
            """;

        // Act
        ExternalOpenBankingDirectDebit directDebit = JsonSerializer.Deserialize<ExternalOpenBankingDirectDebit>(json, WebOptions)!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(directDebit.Meta!.ProviderMandateIdentification).IsEqualTo("mandate-1");
            await Assert.That(directDebit.Meta.ProviderAccountId).IsEqualTo("pacc-1");
        }
    }

    [Test]
    public async Task StandingOrder_DeserialisesMetadata()
    {
        // Arrange
        const string json = """{ "reference": "REF-1", "meta": { "provider_account_id": "pacc-1" } }""";

        // Act
        ExternalOpenBankingAccountStandingOrder standingOrder =
            JsonSerializer.Deserialize<ExternalOpenBankingAccountStandingOrder>(json, WebOptions)!;

        // Assert
        await Assert.That(standingOrder.Meta!.ProviderAccountId).IsEqualTo("pacc-1");
    }

    [Test]
    public async Task ProviderStep_DeserialisesFieldsValuesAndValidations()
    {
        // Arrange
        const string json = """
            {
              "title": "Choose account type",
              "fields": [
                {
                  "type": "select",
                  "id": "account-type",
                  "display_name": "Account type",
                  "help_text": "Pick one",
                  "mandatory": true,
                  "is_sensitive": false,
                  "allowed_characters": "a-z",
                  "values": [ { "value": "personal", "display_name": "Personal" } ],
                  "validations": [ { "regex": "^[a-z]+$", "error_message": "Letters only" } ]
                }
              ]
            }
            """;

        // Act
        ExternalOpenBankingProviderStep step = JsonSerializer.Deserialize<ExternalOpenBankingProviderStep>(json, WebOptions)!;

        // Assert
        ExternalOpenBankingProviderStepField field = (await step.Fields!.ToListAsync(CancellationToken)).Single();
        ExternalOpenBankingProviderFieldValue value = (await field.Values!.ToListAsync(CancellationToken)).Single();
        ExternalOpenBankingProviderStepValidation validation =
            (await field.Validations!.ToListAsync(CancellationToken)).Single();

        using (Assert.Multiple())
        {
            await Assert.That(step.Title).IsEqualTo("Choose account type");
            await Assert.That(field.Type).IsEqualTo("select");
            await Assert.That(field.Id).IsEqualTo("account-type");
            await Assert.That(field.DisplayName).IsEqualTo("Account type");
            await Assert.That(field.HelpText).IsEqualTo("Pick one");
            await Assert.That(field.Mandatory).IsTrue();
            await Assert.That(field.IsSensitive).IsFalse();
            await Assert.That(field.AllowedCharacters).IsEqualTo("a-z");
            await Assert.That(value.Value).IsEqualTo("personal");
            await Assert.That(value.DisplayName).IsEqualTo("Personal");
            await Assert.That(validation.Regex).IsEqualTo("^[a-z]+$");
            await Assert.That(validation.ErrorMessage).IsEqualTo("Letters only");
        }
    }
}
