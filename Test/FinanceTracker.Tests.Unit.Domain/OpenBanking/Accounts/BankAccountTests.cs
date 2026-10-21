using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Accounts;

[Category("Domain")]
public class BankAccountTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BankAccount_ValidConstruction_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        var account = new BankAccount(userId, connectionId, "acc-1", "TRANSACTION", "Main", "GBP");

        using (Assert.Multiple())
        {
            await Assert.That(account.UserId).IsEqualTo(userId);
            await Assert.That(account.ConnectionId).IsEqualTo(connectionId);
            await Assert.That(account.ExternalId).IsEqualTo("acc-1");
            await Assert.That(account.AccountType).IsEqualTo("TRANSACTION");
            await Assert.That(account.DisplayName).IsEqualTo("Main");
            await Assert.That(account.Currency).IsEqualTo("GBP");
            await Assert.That(account.Balance).IsNull();
            await Assert.That(account.StandingOrders.Count).IsEqualTo(0);
            await Assert.That(account.DirectDebits.Count).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("", "TRANSACTION", "Main", "GBP")]
    [Arguments("acc-1", "", "Main", "GBP")]
    [Arguments("acc-1", "TRANSACTION", " ", "GBP")]
    [Arguments("acc-1", "TRANSACTION", "Main", "")]
    public void BankAccount_InvalidConstruction_Throws_ArgumentException(string externalId, string accountType,
        string displayName, string currency)
    {
        Assert.Throws<ArgumentException>(() =>
            new BankAccount(Guid.NewGuid(), Guid.NewGuid(), externalId, accountType, displayName, currency));
    }

    [Test]
    public void BankAccount_EmptyConnectionId_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BankAccount(Guid.NewGuid(), Guid.Empty, "acc-1", "TRANSACTION", "Main", "GBP"));
    }

    [Test]
    public async Task BankAccount_UpdateDetails_ReplacesDetails()
    {
        var account = BuildAccount();

        account.UpdateDetails("SAVINGS", "Rainy Day", "EUR");

        using (Assert.Multiple())
        {
            await Assert.That(account.AccountType).IsEqualTo("SAVINGS");
            await Assert.That(account.DisplayName).IsEqualTo("Rainy Day");
            await Assert.That(account.Currency).IsEqualTo("EUR");
        }
    }

    [Test]
    [Arguments("", "Main", "GBP")]
    [Arguments("SAVINGS", "", "GBP")]
    [Arguments("SAVINGS", "Main", "")]
    public void BankAccount_UpdateDetails_Invalid_Throws_ArgumentException(string accountType, string displayName,
        string currency)
    {
        var account = BuildAccount();

        Assert.Throws<ArgumentException>(() =>
            account.UpdateDetails(accountType, displayName, currency));
    }

    [Test]
    public async Task BankAccount_UpdateBalance_SetsBalance()
    {
        var account = BuildAccount();

        account.UpdateBalance(90m, -10m, Now);

        await Assert.That(account.Balance).IsEqualTo(new AccountBalance(90m, -10m, Now));
    }

    [Test]
    public async Task BankAccount_SyncStandingOrders_AddsNewOrders()
    {
        var account = BuildAccount();
        var details = StandingOrder("REF-1", "Landlord", 750m);

        account.SyncStandingOrders([details]);

        var standingOrder = account.StandingOrders.Single();

        using (Assert.Multiple())
        {
            await Assert.That(standingOrder.Reference).IsEqualTo("REF-1");
            await Assert.That(standingOrder.Payee).IsEqualTo("Landlord");
            await Assert.That(standingOrder.Frequency).IsEqualTo(details.Frequency);
            await Assert.That(standingOrder.Status).IsEqualTo(details.Status);
            await Assert.That(standingOrder.Currency).IsEqualTo(details.Currency);
            await Assert.That(standingOrder.NextPaymentDate).IsEqualTo(details.NextPaymentDate);
            await Assert.That(standingOrder.NextPaymentAmount).IsEqualTo(750m);
            await Assert.That(standingOrder.FirstPaymentDate).IsEqualTo(details.FirstPaymentDate);
            await Assert.That(standingOrder.FirstPaymentAmount).IsEqualTo(details.FirstPaymentAmount);
            await Assert.That(standingOrder.FinalPaymentDate).IsEqualTo(details.FinalPaymentDate);
            await Assert.That(standingOrder.FinalPaymentAmount).IsEqualTo(details.FinalPaymentAmount);
            await Assert.That(standingOrder.Timestamp).IsEqualTo(details.Timestamp);
        }
    }

    [Test]
    public async Task BankAccount_SyncStandingOrders_MatchesOnReferenceAndPayee()
    {
        var account = BuildAccount();
        account.SyncStandingOrders([StandingOrder("REF-1", "Landlord", 750m)]);
        var original = account.StandingOrders.Single();

        account.SyncStandingOrders([StandingOrder("REF-1", "Landlord", 800m)]);

        var updated = account.StandingOrders.Single();

        using (Assert.Multiple())
        {
            await Assert.That(updated).IsSameReferenceAs(original);
            await Assert.That(updated.NextPaymentAmount).IsEqualTo(800m);
        }
    }

    [Test]
    public async Task BankAccount_SyncStandingOrders_SameReferenceDifferentPayee_IsSeparateOrder()
    {
        var account = BuildAccount();
        account.SyncStandingOrders([StandingOrder("REF-1", "Landlord", 750m)]);
        var original = account.StandingOrders.Single();

        account.SyncStandingOrders([StandingOrder("REF-1", "New Landlord", 750m)]);

        using (Assert.Multiple())
        {
            await Assert.That(account.StandingOrders.Single()).IsNotSameReferenceAs(original);
            await Assert.That(account.StandingOrders.Single().Payee).IsEqualTo("New Landlord");
        }
    }

    [Test]
    public async Task BankAccount_SyncStandingOrders_RemovesOrdersNoLongerReported()
    {
        var account = BuildAccount();
        account.SyncStandingOrders([StandingOrder("REF-1", "Landlord", 750m), StandingOrder("REF-2", "Gym", 30m)]);

        account.SyncStandingOrders([StandingOrder("REF-2", "Gym", 30m)]);

        await Assert.That(account.StandingOrders.Select(x => x.Reference)).IsEquivalentTo(["REF-2"]);
    }

    [Test]
    public async Task BankAccount_SyncStandingOrders_Empty_RemovesAll()
    {
        var account = BuildAccount();
        account.SyncStandingOrders([StandingOrder("REF-1", "Landlord", 750m)]);

        account.SyncStandingOrders([]);

        await Assert.That(account.StandingOrders.Count).IsEqualTo(0);
    }

    [Test]
    public void BankAccount_SyncStandingOrders_Null_Throws_ArgumentNullException()
    {
        var account = BuildAccount();

        Assert.Throws<ArgumentNullException>(() =>
            account.SyncStandingOrders(null!));
    }

    [Test]
    public async Task BankAccount_SyncDirectDebits_AddsNewMandates()
    {
        var account = BuildAccount();
        var details = DirectDebit("dd-1", "Energy", 80m);

        account.SyncDirectDebits([details]);

        var directDebit = account.DirectDebits.Single();

        using (Assert.Multiple())
        {
            await Assert.That(directDebit.ExternalId).IsEqualTo("dd-1");
            await Assert.That(directDebit.Name).IsEqualTo("Energy");
            await Assert.That(directDebit.Status).IsEqualTo(details.Status);
            await Assert.That(directDebit.PreviousPaymentDate).IsEqualTo(details.PreviousPaymentDate);
            await Assert.That(directDebit.PreviousPaymentAmount).IsEqualTo(80m);
            await Assert.That(directDebit.Currency).IsEqualTo(details.Currency);
            await Assert.That(directDebit.Timestamp).IsEqualTo(details.Timestamp);
        }
    }

    [Test]
    public async Task BankAccount_SyncDirectDebits_MatchesOnExternalId()
    {
        var account = BuildAccount();
        account.SyncDirectDebits([DirectDebit("dd-1", "Energy", 80m)]);
        var original = account.DirectDebits.Single();

        account.SyncDirectDebits([DirectDebit("dd-1", "Energy Co", 95m)]);

        var updated = account.DirectDebits.Single();

        using (Assert.Multiple())
        {
            await Assert.That(updated).IsSameReferenceAs(original);
            await Assert.That(updated.Name).IsEqualTo("Energy Co");
            await Assert.That(updated.PreviousPaymentAmount).IsEqualTo(95m);
        }
    }

    [Test]
    public async Task BankAccount_SyncDirectDebits_RemovesMandatesNoLongerReported()
    {
        var account = BuildAccount();
        account.SyncDirectDebits([DirectDebit("dd-1", "Energy", 80m), DirectDebit("dd-2", "Water", 20m)]);

        account.SyncDirectDebits([DirectDebit("dd-1", "Energy", 80m)]);

        await Assert.That(account.DirectDebits.Select(x => x.ExternalId)).IsEquivalentTo(["dd-1"]);
    }

    [Test]
    public async Task BankAccount_SyncDirectDebits_NullCurrency_IsAllowed()
    {
        var account = BuildAccount();

        account.SyncDirectDebits([DirectDebit("dd-1", "Energy", 80m) with { Currency = null }]);

        await Assert.That(account.DirectDebits.Single().Currency).IsNull();
    }

    [Test]
    public void BankAccount_SyncDirectDebits_EmptyExternalId_Throws_ArgumentException()
    {
        var account = BuildAccount();

        Assert.Throws<ArgumentException>(() =>
            account.SyncDirectDebits([DirectDebit("", "Energy", 80m)]));
    }

    [Test]
    public void BankAccount_SyncDirectDebits_Null_Throws_ArgumentNullException()
    {
        var account = BuildAccount();

        Assert.Throws<ArgumentNullException>(() =>
            account.SyncDirectDebits(null!));
    }

    [Test]
    [Arguments(SyncType.Account)]
    [Arguments(SyncType.Transactions)]
    public async Task BankAccount_RecordSync_CreatesEntryForAccount(SyncType type)
    {
        var account = BuildAccount();

        var entry = account.RecordSync(type, Now);

        using (Assert.Multiple())
        {
            await Assert.That(entry.AccountId).IsEqualTo(account.Id);
            await Assert.That(entry.Type).IsEqualTo(type);
            await Assert.That(entry.OccurredAt).IsEqualTo(Now);
            await Assert.That(entry.Id).IsNotEqualTo(Guid.Empty);
        }
    }

    private static BankAccount BuildAccount() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    private static StandingOrderDetails StandingOrder(string reference, string payee, decimal amount) =>
        new(reference, payee, "Monthly", "Active", "GBP", Now.AddDays(5), amount, Now.AddYears(-1), amount,
            Now.AddYears(1), amount, Now);

    private static DirectDebitDetails DirectDebit(string externalId, string name, decimal amount) =>
        new(externalId, name, "Active", Now.AddDays(-5), amount, "GBP", Now);
}
