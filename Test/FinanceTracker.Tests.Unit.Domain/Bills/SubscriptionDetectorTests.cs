using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.Bills;

[Category("Domain")]
public class SubscriptionDetectorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "CREDIT_CARD", "Card", "GBP");
    private static readonly BankAccount Other = new(UserId, Guid.NewGuid(), "acc-2", "TRANSACTION", "Main", "GBP");
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Test]
    public async Task Detect_ThreeMonthlyPayments_SuggestsThem()
    {
        // Arrange
        List<BankTransaction> payments =
        [
            Card("NETFLIX.COM 866-111", -10.99m, 8, 12),
            Card("NETFLIX.COM 866-222", -10.99m, 9, 12),
            Card("NETFLIX.COM 866-333", -10.99m, 10, 13)
        ];

        // Act
        DetectedSubscription detected = SubscriptionDetector.Detect([], payments, TimeZoneInfo.Utc, Today).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(detected.AccountId).IsEqualTo(Account.Id);
            await Assert.That(detected.Name).IsEqualTo("NETFLIX.COM");
            await Assert.That(detected.Key).IsEqualTo("NETFLIXCOM");
            await Assert.That(detected.Frequency).IsEqualTo(BillFrequency.Monthly);
            await Assert.That(detected.Amount).IsEqualTo(10.99m);
            await Assert.That(detected.FirstPaidOn).IsEqualTo(new DateOnly(2026, 8, 12));
            await Assert.That(detected.LastPaidOn).IsEqualTo(new DateOnly(2026, 10, 13));
            await Assert.That(detected.PaymentIds).IsEquivalentTo(payments.Select(static x => x.Id));
        }
    }

    [Test]
    public async Task Detect_MerchantName_IsThePayee()
    {
        // Arrange
        TransactionMetadata metadata = new(null, null, "Spotify", null, null);
        List<BankTransaction> payments =
        [
            Card("CARD 1", -11.99m, 8, 1, metadata), Card("CARD 2", -11.99m, 9, 1, metadata),
            Card("CARD 3", -11.99m, 10, 1, metadata)
        ];

        // Act
        DetectedSubscription detected = SubscriptionDetector.Detect([], payments, TimeZoneInfo.Utc, Today).Single();

        // Assert
        await Assert.That(detected.Name).IsEqualTo("Spotify");
    }

    [Test]
    public async Task Detect_EveryFourWeeks_IsFourWeekly()
    {
        // Arrange
        List<BankTransaction> payments =
        [
            Card("GYM", -30m, 8, 21), Card("GYM", -30m, 9, 18), Card("GYM", -30m, 10, 16)
        ];

        // Act
        DetectedSubscription detected = SubscriptionDetector.Detect([], payments, TimeZoneInfo.Utc, Today).Single();

        // Assert
        await Assert.That(detected.Frequency).IsEqualTo(BillFrequency.FourWeekly);
    }

    [Test]
    public async Task Detect_Weekly_IsWeekly()
    {
        // Arrange
        List<BankTransaction> payments =
        [
            Card("VEG BOX", -18m, 10, 9), Card("VEG BOX", -18m, 10, 16), Card("VEG BOX", -18m, 10, 23)
        ];

        // Act
        DetectedSubscription detected = SubscriptionDetector.Detect([], payments, TimeZoneInfo.Utc, Today).Single();

        // Assert
        await Assert.That(detected.Frequency).IsEqualTo(BillFrequency.Weekly);
    }

    [Test]
    public async Task Detect_TwoPayments_SuggestsNothing() =>
        await Assert.That(SubscriptionDetector.Detect([],
            [Card("NETFLIX", -10.99m, 9, 12), Card("NETFLIX", -10.99m, 10, 12)], TimeZoneInfo.Utc, Today)).IsEmpty();

    [Test]
    public async Task Detect_AmountsTooDifferent_SuggestsNothing() =>
        await Assert.That(SubscriptionDetector.Detect([],
        [
            Card("SUPERMARKET", -40m, 8, 12), Card("SUPERMARKET", -75m, 9, 12), Card("SUPERMARKET", -52m, 10, 12)
        ], TimeZoneInfo.Utc, Today)).IsEmpty();

    [Test]
    public async Task Detect_IrregularDates_SuggestsNothing() =>
        await Assert.That(SubscriptionDetector.Detect([],
        [
            Card("TAKEAWAY", -20m, 8, 2), Card("TAKEAWAY", -20m, 8, 19), Card("TAKEAWAY", -20m, 10, 3)
        ], TimeZoneInfo.Utc, Today)).IsEmpty();

    [Test]
    public async Task Detect_StoppedMonthsAgo_SuggestsNothing() =>
        await Assert.That(SubscriptionDetector.Detect([],
        [
            Card("MAGAZINE", -4.99m, 5, 1), Card("MAGAZINE", -4.99m, 6, 1), Card("MAGAZINE", -4.99m, 7, 1)
        ], TimeZoneInfo.Utc, Today)).IsEmpty();

    [Test]
    public async Task Detect_SamePayeeOnTwoAccounts_KeepsThemApart() =>
        await Assert.That(SubscriptionDetector.Detect([],
        [
            Card("NETFLIX", -10.99m, 8, 12), Card("NETFLIX", -10.99m, 9, 12),
            Card("NETFLIX", -10.99m, 10, 12, account: Other)
        ], TimeZoneInfo.Utc, Today)).IsEmpty();

    [Test]
    public async Task Detect_AlreadyABill_SuggestsNothing()
    {
        // Arrange
        Bill bill = new(UserId, Account.Id, "Netflix", "Netflix", BillFrequency.Monthly, 10.99m,
            new DateOnly(2026, 1, 12), BillSource.Manual);

        // Act & Assert
        await Assert.That(SubscriptionDetector.Detect([bill],
        [
            Card("NETFLIX", -10.99m, 8, 1), Card("NETFLIX", -10.99m, 9, 1), Card("NETFLIX", -10.99m, 10, 1)
        ], TimeZoneInfo.Utc, Today)).IsEmpty();
    }

    [Test]
    public async Task Detect_PendingOrPayingABill_LeftOut()
    {
        // Arrange
        BankTransaction paid = Card("NETFLIX", -10.99m, 10, 12);
        paid.DecideBillPayment(BillPayment.Matched(Guid.NewGuid(), DateTime.UtcNow));

        // Act & Assert
        await Assert.That(SubscriptionDetector.Detect([],
        [
            Card("NETFLIX", -10.99m, 8, 12), Card("NETFLIX", -10.99m, 9, 12, pending: true), paid
        ], TimeZoneInfo.Utc, Today)).IsEmpty();
    }

    [Test]
    public async Task Suggest_NewlyDetected_AddsASuggestion()
    {
        // Act
        SuggestionChanges changes = SubscriptionDetector.Suggest(UserId, [], [Detected()]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changes.Added).Count().IsEqualTo(1);
            await Assert.That(changes.Added[0].UserId).IsEqualTo(UserId);
            await Assert.That(changes.Removed).IsEmpty();
        }
    }

    [Test]
    public async Task Suggest_AlreadySuggested_RefreshesIt()
    {
        // Arrange
        BillSuggestion existing = new(UserId, Detected(amount: 10.99m));

        // Act
        SuggestionChanges changes = SubscriptionDetector.Suggest(UserId, [existing], [Detected(amount: 12.99m)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changes.Added).IsEmpty();
            await Assert.That(changes.Removed).IsEmpty();
            await Assert.That(existing.Amount).IsEqualTo(12.99m);
        }
    }

    [Test]
    public async Task Suggest_Dismissed_StaysDismissedAndIsntAddedAgain()
    {
        // Arrange
        BillSuggestion dismissed = new(UserId, Detected(amount: 10.99m));
        dismissed.Dismiss();

        // Act
        SuggestionChanges changes = SubscriptionDetector.Suggest(UserId, [dismissed], [Detected(amount: 12.99m)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changes.Added).IsEmpty();
            await Assert.That(changes.Removed).IsEmpty();
            await Assert.That(dismissed.Amount).IsEqualTo(10.99m);
        }
    }

    [Test]
    public async Task Suggest_NoLongerDetected_RemovesItUnlessDismissed()
    {
        // Arrange
        BillSuggestion waiting = new(UserId, Detected());
        BillSuggestion dismissed = new(UserId, Detected() with { Key = "SPOTIFY", Name = "Spotify" });
        dismissed.Dismiss();

        // Act
        SuggestionChanges changes = SubscriptionDetector.Suggest(UserId, [waiting, dismissed], []);

        // Assert
        await Assert.That(changes.Removed).IsEquivalentTo([waiting]);
    }

    [Test]
    public async Task ToBill_IsADetectedBillOnTheLatestPaymentsSchedule()
    {
        // Arrange
        BillSuggestion suggestion = new(UserId, Detected());

        // Act
        Bill bill = suggestion.ToBill();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bill.Source).IsEqualTo(BillSource.Detected);
            await Assert.That(bill.Name).IsEqualTo("NETFLIX.COM");
            await Assert.That(bill.StartsOn).IsEqualTo(new DateOnly(2026, 8, 12));
            await Assert.That(bill.NextDue(Today)).IsEqualTo(new DateOnly(2026, 11, 13));
            await Assert.That(bill.ExpectedAmount).IsEqualTo(10.99m);
        }
    }

    [Test]
    public async Task ToBill_Dismissed_Throws()
    {
        // Arrange
        BillSuggestion suggestion = new(UserId, Detected());
        suggestion.Dismiss();

        // Act & Assert
        await Assert.That(suggestion.ToBill).Throws<InvalidOperationException>();
    }

    private static DetectedSubscription Detected(decimal amount = 10.99m) =>
        new(Account.Id, "NETFLIXCOM", "NETFLIX.COM", BillFrequency.Monthly, amount, new DateOnly(2026, 8, 12),
            new DateOnly(2026, 10, 13), [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]);

    private static BankTransaction Card(string description, decimal amount, int month, int day,
        TransactionMetadata? metadata = null, bool pending = false, BankAccount? account = null) =>
        BankTransaction.Import(account ?? Account, new TransactionDetails(Guid.NewGuid().ToString(), description,
            TransactionType.Debit, "PURCHASE", amount, "GBP", new DateTime(2026, month, day, 12, 0, 0,
                DateTimeKind.Utc), pending, [], metadata ?? TransactionMetadata.None, false));
}
