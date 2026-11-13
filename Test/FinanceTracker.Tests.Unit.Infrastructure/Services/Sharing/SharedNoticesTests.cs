using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.HouseholdMembers;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// What Alex is told about what Sam shares, and about Sam changing it. Sam shares Joint with Alex to see, and keeps
/// Current, sort code 112233 and number 87654321, to themselves. Alex has an account of their own, Everyday. Chris
/// isn't linked with either.
/// </summary>
public class SharedNoticesTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";
    private const string Chris = "chris";

    private readonly List<(Guid UserId, NotificationKind Kind, string Title)> _raised = [];
    private ServiceProvider _services = null!;
    private Guid _linkId;

    [Before(Test)]
    public async Task SeedAsync()
    {
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), _raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();

        await Scenario.Do(s => s.UserScenario.WithUser("Alex", "Smith", "alex@example.test", Alex))
            .Do(s => s.UserScenario.WithUser("Sam", "Taylor", "sam@example.test", Sam))
            .Do(s => s.UserScenario.WithUser("Chris", "Jones", "chris@example.test", Chris))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-everyday", "alex-bank", Named("Everyday")))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-current", "sam-bank", (userId, connectionId) =>
            {
                BankAccount current = Named("Current")(userId, connectionId);
                current.UpdateIdentifier(AccountIdentifier.Create("112233", "87654321", null));
                return current;
            }))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = ShareMode.ReadOnly }]
        }, CancellationToken);
        _raised.Clear();
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task ABillOnASharedAccount_DueSoon_RemindsAlexToo_SayingWhose()
    {
        // Arrange
        DateOnly tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await Scenario.Do(s => s.BillScenario.WithBill("rent", Sam, "sam-joint", (userId, accountId) =>
            new Bill(userId, accountId, "Rent", "LANDLORD", BillFrequency.Monthly, 800m, tomorrow,
                BillSource.Manual)));

        // Act
        await BillNotices(Alex, TimeProvider.System).NotifyAsync(CancellationToken);

        // Assert
        await Assert.That(_raised).IsEquivalentTo([(UserOf(Alex), NotificationKind.BillDueSoon,
            "Sam Taylor's Rent is due tomorrow")]);
    }

    [Test]
    [Arguments(true, 0)]
    [Arguments(false, 1)]
    public async Task ABillSharedByChoice_PaidFromAnAccountAlexCantSee_IsOnlyLateForAlexWhenItIs(bool paid,
        int late)
    {
        // Arrange, due a week ago, from Sam's Current, which Alex can't see
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);
        DateOnly due = today.AddDays(-7);
        await Scenario.Do(s => s.BillScenario.WithBill("rent", Sam, "sam-current", (userId, accountId) =>
            new Bill(userId, accountId, "Rent", "LANDLORD", BillFrequency.Monthly, 800m, due, BillSource.Manual)));
        if (paid)
        {
            await Scenario.Do(s => s.TransactionScenario.WithTransaction("payment", "sam-current",
                    Details(-800m, "LANDLORD", due.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc))))
                .Do(s => s.TransactionScenario.WithBillPayment("payment", "rent"));
        }

        await Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items =
            [
                new ItemShareChoice
                {
                    Kind = SharedItemKind.Bill, ItemId = Scenario.BillMappings["rent"], Mode = ShareMode.ReadOnly
                }
            ]
        }, CancellationToken);
        _raised.Clear();

        // Act
        await BillNotices(Alex, new FixedTimeProvider(today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            TimeZoneInfo.Utc)).NotifyAsync(CancellationToken);

        // Assert
        await Assert.That(_raised.Count(static x => x.Kind == NotificationKind.BillLate)).IsEqualTo(late);
    }

    [Test]
    public async Task ASharedAccountAlexWatches_GoesLow_TellsAlex_SayingWhose()
    {
        // Arrange
        using CultureScope culture = new();
        await Scenario.Do(s => s.BankAccountScenario.WithBalance("sam-joint",
            new AccountBalance(40m, 40m, DateTime.UtcNow)));
        await WithAlexsSettingsAsync(x =>
            x.WatchBalances(new Dictionary<Guid, decimal> { [Account("sam-joint")] = 100m }));

        // Act
        await new BalanceNotices(CurrentUserFor(Alex), DbContextFactory, NullLogger<BalanceNotices>.Instance,
            TimeProvider.System, Notifier(Alex)).NotifyAsync(CancellationToken);

        // Assert
        await Assert.That(_raised).IsEquivalentTo([(UserOf(Alex), NotificationKind.LowBalance,
            "Joint · Sam Taylor's is below £100.00")]);
    }

    [Test]
    public async Task ALargePaymentFromASharedAccount_TellsAlex()
    {
        // Arrange
        using CultureScope culture = new();
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("sofa", "sam-joint",
            Details(-750m, "SOFA SHOP", DateTime.UtcNow.AddHours(-2))));
        await WithAlexsSettingsAsync(x => x.ChangeAlerts(NotificationSettings.DefaultBudgetWarningPercent,
            NotificationSettings.DefaultBillReminderDays, 500m));

        // Act
        await new TransactionNotices(CurrentUserFor(Alex), DbContextFactory, NullLogger<TransactionNotices>.Instance,
            TimeProvider.System, Notifier(Alex)).NotifyAsync(CancellationToken);

        // Assert
        await Assert.That(_raised).IsEquivalentTo([(UserOf(Alex), NotificationKind.LargeTransaction,
            "£750.00 paid to SOFA SHOP")]);
    }

    [Test]
    public async Task SamSharingThenStopping_TellsAlexEachTime_AndEndingTellsThemItsOver()
    {
        // Act
        await ShareTagAsync(ShareMode.ReadOnly);
        await ShareTagAsync(ShareMode.ReadOnly);
        await Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = []
        }, CancellationToken);
        await Sharing(Sam).EndAsync(_linkId, CancellationToken);

        // Assert, saving the same again says nothing
        await Assert.That(_raised.Select(static x => (x.UserId, x.Kind, x.Title))).IsEquivalentTo(
        [
            (UserOf(Alex), NotificationKind.SharingChanged, "Sam Taylor changed what they share with you"),
            (UserOf(Alex), NotificationKind.SharingChanged, "Sam Taylor changed what they share with you"),
            (UserOf(Alex), NotificationKind.SharingChanged, "Sam Taylor stopped sharing with you")
        ], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DecliningAndWithdrawingInvites_TellTheOtherPerson()
    {
        // Arrange
        InviteOutcome toAlex = await Sharing(Chris).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        InviteOutcome toSam = await Sharing(Chris).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);
        _raised.Clear();

        // Act
        await Sharing(Alex).DeclineAsync(toAlex.Link!.Id, CancellationToken);
        await Sharing(Chris).EndAsync(toSam.Link!.Id, CancellationToken);

        // Assert
        await Assert.That(_raised).IsEquivalentTo(
        [
            (UserOf(Chris), NotificationKind.SharingChanged, "Alex Smith declined your invite to share"),
            (UserOf(Sam), NotificationKind.SharingChanged, "Chris Jones withdrew their invite to share")
        ]);
    }

    [Test]
    public async Task AMemberWhoIsSam_RecognisesMoneyToSamsOwnAccount_UntilUnlinked()
    {
        // Arrange, Alex pays into Sam's Current, which Sam hasn't shared
        TransactionDetails toSam = Details(-50m, "MONEY FOR SAM", DateTime.UtcNow.AddHours(-1)) with
        {
            Metadata = new TransactionMetadata("112233 87654321", null, null, null, null)
        };
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("to-sam", "alex-everyday", toSam));
        HouseholdMember partner = await Household(Alex).AddHouseholdMemberAsync(new AddHouseholdMember
        {
            FirstName = "My", LastName = "Partner", YearlyIncome = 0m
        }, CancellationToken);

        // Act
        await Household(Alex).LinkHouseholdMemberAsync(partner.Id, new LinkHouseholdMember { LinkId = _linkId },
            CancellationToken);
        Guid? whileLinked = await HouseholdMemberOfAsync("to-sam");
        await Household(Alex).LinkHouseholdMemberAsync(partner.Id, new LinkHouseholdMember { LinkId = null },
            CancellationToken);
        Guid? afterwards = await HouseholdMemberOfAsync("to-sam");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(whileLinked).IsEqualTo(partner.Id);
            await Assert.That(afterwards).IsNull();
        }
    }

    private async Task<Guid?> HouseholdMemberOfAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.TransactionMappings[transaction];
        BankTransaction found = await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id,
            CancellationToken);
        return found.Transfer?.PartnerHouseholdMemberId;
    }

    private Task<LinkShares> ShareTagAsync(ShareMode mode) =>
        Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = [new ItemShareChoice { Kind = SharedItemKind.Classification, ItemId = Tag(), Mode = mode }]
        }, CancellationToken);

    private async Task WithAlexsSettingsAsync(Action<NotificationSettings> change)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = new(UserOf(Alex));
        change(settings);
        context.NotificationSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken);
    }

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(decimal amount, string description, DateTime occurredAt) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = TransactionType.Debit,
            Currency = "GBP",
            OccurredAt = occurredAt,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = TransactionMetadata.None
        };

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private Guid Tag() => Scenario.ClassificationMappings["groceries"];

    private Guid UserOf(string username) => Scenario.UserMappings[username];

    private RecordingNotifier Notifier(string username) => new(CurrentUserFor(username), _raised);

    private BillNotices BillNotices(string username, TimeProvider time) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BillNotices>.Instance, time, Notifier(username));

    private HouseholdMemberService Household(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<HouseholdMemberService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved));

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
