using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Choosing which accounts to share in a link, and what each side sees of it.
/// </summary>
public class AccountSharingServiceTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";
    private const string Jo = "jo";

    private ServiceProvider _services = null!;

    [Before(Test)]
    public void SetUpNotifier()
    {
        List<(Guid UserId, NotificationKind Kind, string Title)> raised = [];
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();
    }

    [After(Test)]
    public async Task TearDownNotifier() => await _services.DisposeAsync();

    [Test]
    public async Task ChangeAccountSharesAsync_SharesTheChosenAccountsAndTheOtherPersonSeesThem()
    {
        // Arrange
        Guid linkId = await SeedLinkedAsync();

        // Act
        LinkShares saved = await Subject(Alex).ChangeAccountSharesAsync(linkId, Choose(
            ("alex-current", ShareMode.AllowChanges), ("alex-savings", ShareMode.ReadOnly)), CancellationToken);
        LinkShares samSees = (await Subject(Sam).GetSharesAsync(linkId, CancellationToken))!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saved.YourAccounts.Select(x => (x.AccountId, x.Mode))).IsEquivalentTo([
                (Account("alex-current"), (ShareMode?)ShareMode.AllowChanges),
                (Account("alex-savings"), (ShareMode?)ShareMode.ReadOnly),
                (Account("alex-birthday"), (ShareMode?)null)
            ]);
            await Assert.That(samSees.TheirAccounts.Select(static x => x.Mode))
                .IsEquivalentTo([ShareMode.AllowChanges, ShareMode.ReadOnly]);
            await Assert.That(samSees.YourAccounts.All(static x => x.Mode is null)).IsTrue();
        }
    }

    [Test]
    public async Task ChangeAccountSharesAsync_IsTheWholeChoice_LeftOutStopsBeingSharedAndModesChange()
    {
        // Arrange
        Guid linkId = await SeedLinkedAsync();
        await Subject(Alex).ChangeAccountSharesAsync(linkId, Choose(
            ("alex-current", ShareMode.ReadOnly), ("alex-savings", ShareMode.ReadOnly)), CancellationToken);

        // Act
        LinkShares saved = await Subject(Alex).ChangeAccountSharesAsync(linkId,
            Choose(("alex-current", ShareMode.AllowChanges)), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saved.YourAccounts.Where(static x => x.Mode is not null).Select(x => (x.AccountId, x.Mode)))
                .IsEquivalentTo([(Account("alex-current"), (ShareMode?)ShareMode.AllowChanges)]);
            await Assert.That(await CountSharesAsync()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ChangeAccountSharesAsync_SomeoneElsesAccount_ThrowsAndSharesNothing()
    {
        // Arrange, Sam trying to share one of Alex's accounts with Alex
        Guid linkId = await SeedLinkedAsync();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => Subject(Sam).ChangeAccountSharesAsync(linkId,
            Choose(("alex-current", ShareMode.AllowChanges)), CancellationToken));

        // Assert
        await Assert.That(await CountSharesAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task ChangeAccountSharesAsync_ALinkTheyreNotIn_Throws()
    {
        // Arrange
        Guid linkId = await SeedLinkedAsync();

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Subject(Jo).ChangeAccountSharesAsync(linkId,
            Choose(), CancellationToken));
    }

    [Test]
    public async Task GetLinksAsync_CountsWhatsSharedEachWay()
    {
        // Arrange
        Guid linkId = await SeedLinkedAsync();
        await Subject(Alex).ChangeAccountSharesAsync(linkId, Choose(
            ("alex-current", ShareMode.ReadOnly), ("alex-savings", ShareMode.AllowChanges)), CancellationToken);
        await Subject(Sam).ChangeAccountSharesAsync(linkId, Choose(("sam-current", ShareMode.ReadOnly)),
            CancellationToken);

        // Act
        UserLink alexSees = (await Subject(Alex).GetLinksAsync(CancellationToken).ToListAsync(CancellationToken)).Single();
        UserLink samSees = (await Subject(Sam).GetLinksAsync(CancellationToken).ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((alexSees.AccountsYouShare, alexSees.AccountsTheyShare)).IsEqualTo((2, 1));
            await Assert.That((samSees.AccountsYouShare, samSees.AccountsTheyShare)).IsEqualTo((1, 2));
        }
    }

    [Test]
    public async Task GetSharesAsync_NotYetAccepted_IsNull()
    {
        // Arrange
        await SeedUsersAndAccountsAsync();
        InviteOutcome sent = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);

        // Act
        LinkShares? shares = await Subject(Alex).GetSharesAsync(sent.Link!.Id, CancellationToken);

        // Assert
        await Assert.That(shares).IsNull();
    }

    [Test]
    public async Task GetSharesAsync_ALinkTheyreNotIn_IsNull()
    {
        // Arrange
        Guid linkId = await SeedLinkedAsync();

        // Act
        LinkShares? shares = await Subject(Jo).GetSharesAsync(linkId, CancellationToken);

        // Assert
        await Assert.That(shares).IsNull();
    }

    [Test]
    public async Task EndAsync_RemovesWhatBothShared_SoLinkingAgainSharesNothing()
    {
        // Arrange
        Guid linkId = await SeedLinkedAsync();
        await Subject(Alex).ChangeAccountSharesAsync(linkId, Choose(("alex-current", ShareMode.ReadOnly)),
            CancellationToken);
        await Subject(Sam).ChangeAccountSharesAsync(linkId, Choose(("sam-current", ShareMode.ReadOnly)),
            CancellationToken);

        // Act
        await Subject(Sam).EndAsync(linkId, CancellationToken);
        Guid relinkedId = await LinkAsync();
        LinkShares again = (await Subject(Alex).GetSharesAsync(relinkedId, CancellationToken))!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await CountSharesAsync()).IsEqualTo(0);
            await Assert.That(again.YourAccounts.All(static x => x.Mode is null)).IsTrue();
            await Assert.That(again.TheirAccounts).IsEmpty();
        }
    }

    private async Task<Guid> SeedLinkedAsync()
    {
        await SeedUsersAndAccountsAsync();
        return await LinkAsync();
    }

    private Task<TestingScenario> SeedUsersAndAccountsAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Alex))
            .Do(s => s.UserScenario.WithUser(Sam))
            .Do(s => s.UserScenario.WithUser(Jo))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-current", "alex-bank"))
            .Do(s => s.BankAccountScenario.WithAccount("alex-savings", "alex-bank"))
            .Do(s => s.BankAccountScenario.WithAccount("alex-birthday", "alex-bank"))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-current", "sam-bank"));

    private async Task<Guid> LinkAsync()
    {
        InviteOutcome sent = await Subject(Alex).InviteAsync(new InviteToShare { Who = Sam }, CancellationToken);
        await Subject(Sam).AcceptAsync(sent.Link!.Id, CancellationToken);
        return sent.Link.Id;
    }

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private ChangeAccountShares Choose(params (string Account, ShareMode Mode)[] accounts) => new()
    {
        Accounts = accounts.Select(x => new AccountShareChoice { AccountId = Account(x.Account), Mode = x.Mode }).ToList()
    };

    private async Task<int> CountSharesAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Shares.CountAsync(CancellationToken);
    }

    private SharingService Subject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
