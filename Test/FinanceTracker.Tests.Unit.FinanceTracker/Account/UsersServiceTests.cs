using FinanceTracker.Components.Account;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Account;

/// <summary>
/// An admin, Alex, giving and taking the admin role, locking people and turning off their two-factor sign in, over
/// Identity's stores.
/// </summary>
public class UsersServiceTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private readonly ISecurityEmails _securityEmails = Substitute.For<ISecurityEmails>();
    private ServiceProvider _services = null!;
    private AsyncServiceScope _scope;

    private UserManager<FinanceTrackerUser> UserManager =>
        _scope.ServiceProvider.GetRequiredService<UserManager<FinanceTrackerUser>>();

    [Before(Test)]
    public async Task SetupAsync()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped(_ => DbContextFactory.CreateDbContext());
        services.AddIdentityCore<FinanceTrackerUser>()
            .AddRoles<FinanceTrackerRole>()
            .AddEntityFrameworkStores<FinanceTrackerContext>();

        _services = services.BuildServiceProvider();
        _scope = _services.CreateAsyncScope();

        await Scenario.Do(s => s.UserScenario.WithUser(Alex))
            .Do(s => s.UserScenario.WithUser(Sam))
            .Do(s => s.UserScenario.WithRole(Alex, FinTrackRoles.Admin));
    }

    [After(Test)]
    public async Task CleanupAsync()
    {
        await _scope.DisposeAsync();
        await _services.DisposeAsync();
    }

    [Test]
    public async Task GrantAdminAsync_APasswordUser_MakesThemAdminWithoutSigningThemOut()
    {
        // Arrange
        string stampBefore = (await FindAsync(Sam)).SecurityStamp!;

        // Act
        AdminChange change = await Subject().GrantAdminAsync(Id(Sam), CancellationToken);

        // Assert
        FinanceTrackerUser sam = await FindAsync(Sam);
        using (Assert.Multiple())
        {
            await Assert.That(change).IsEqualTo(AdminChange.Changed);
            await Assert.That(await UserManager.IsInRoleAsync(sam, FinTrackRoles.Admin)).IsTrue();
            await Assert.That(sam.SecurityStamp).IsEqualTo(stampBefore);
        }
    }

    [Test]
    public async Task GrantAdminAsync_SomeoneWhoSignsInThroughTheIdentityProvider_IsRefused()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithOidcSubject(Sam, "sam-at-the-provider"));

        // Act
        AdminChange change = await Subject().GrantAdminAsync(Id(Sam), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(change).IsEqualTo(AdminChange.FromIdentityProvider);
            await Assert.That(await UserManager.IsInRoleAsync(await FindAsync(Sam), FinTrackRoles.Admin)).IsFalse();
        }
    }

    [Test]
    public async Task RemoveAdminAsync_AnotherAdmin_TakesTheirRole()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithRole(Sam, FinTrackRoles.Admin));

        // Act
        AdminChange change = await Subject().RemoveAdminAsync(Id(Sam), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(change).IsEqualTo(AdminChange.Changed);
            await Assert.That(await UserManager.IsInRoleAsync(await FindAsync(Sam), FinTrackRoles.Admin)).IsFalse();
        }
    }

    [Test]
    public async Task RemoveAdminAsync_TheirOwnWithAnotherAdmin_TakesIt()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithRole(Sam, FinTrackRoles.Admin));

        // Act
        AdminChange change = await Subject().RemoveAdminAsync(Id(Alex), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(change).IsEqualTo(AdminChange.Changed);
            await Assert.That(await UserManager.IsInRoleAsync(await FindAsync(Alex), FinTrackRoles.Admin)).IsFalse();
        }
    }

    [Test]
    public async Task RemoveAdminAsync_TheLastAdmin_IsRefused()
    {
        // Act
        AdminChange change = await Subject().RemoveAdminAsync(Id(Alex), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(change).IsEqualTo(AdminChange.LastAdmin);
            await Assert.That(await UserManager.IsInRoleAsync(await FindAsync(Alex), FinTrackRoles.Admin)).IsTrue();
        }
    }

    [Test]
    public async Task RemoveAdminAsync_TheLastAdminWithPasswordSignInOff_TakesTheRole()
    {
        // Arrange, the identity provider's admins group is the way back in

        // Act
        AdminChange change = await Subject(passwordSignIn: false).RemoveAdminAsync(Id(Alex), CancellationToken);

        // Assert
        await Assert.That(change).IsEqualTo(AdminChange.Changed);
    }

    [Test]
    public async Task RemoveAdminAsync_SomeoneWhoSignsInThroughTheIdentityProvider_IsRefused()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithRole(Sam, FinTrackRoles.Admin))
            .Do(s => s.UserScenario.WithOidcSubject(Sam, "sam-at-the-provider"));

        // Act
        AdminChange change = await Subject().RemoveAdminAsync(Id(Sam), CancellationToken);

        // Assert
        await Assert.That(change).IsEqualTo(AdminChange.FromIdentityProvider);
    }

    [Test]
    public async Task LockAsync_AnAdmin_IsRefused()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithRole(Sam, FinTrackRoles.Admin));
        UsersService subject = Subject();

        // Act
        bool lockedSam = await subject.LockAsync(Id(Sam), CancellationToken);
        bool lockedThemselves = await subject.LockAsync(Id(Alex), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(lockedSam).IsFalse();
            await Assert.That(lockedThemselves).IsFalse();
            await Assert.That((await FindAsync(Sam)).IsLockedByAdmin).IsFalse();
        }
    }

    [Test]
    public async Task LockAsync_SomeoneWhosNotAnAdmin_LocksThem()
    {
        // Act
        bool locked = await Subject().LockAsync(Id(Sam), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(locked).IsTrue();
            await Assert.That((await FindAsync(Sam)).IsLockedByAdmin).IsTrue();
        }
    }

    [Test]
    public async Task TurnOffTwoFactorAsync_SomeoneWithItOn_ReplacesTheirKeyAndCodesSignsThemOutAndEmailsThem()
    {
        // Arrange, Sam has two-factor on with a key and recovery codes
        FinanceTrackerUser sam = (await UserManager.FindByIdAsync(Id(Sam).ToString()))!;
        await UserManager.ResetAuthenticatorKeyAsync(sam);
        string keyBefore = (await UserManager.GetAuthenticatorKeyAsync(sam))!;
        await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(sam, 10);
        await UserManager.SetTwoFactorEnabledAsync(sam, true);
        string stampBefore = (await FindAsync(Sam)).SecurityStamp!;

        // Act
        bool turnedOff = await Subject().TurnOffTwoFactorAsync(Id(Sam), CancellationToken);

        // Assert
        FinanceTrackerUser after = await FindAsync(Sam);
        using (Assert.Multiple())
        {
            await Assert.That(turnedOff).IsTrue();
            await Assert.That(after.TwoFactorEnabled).IsFalse();
            await Assert.That(after.SecurityStamp).IsNotEqualTo(stampBefore);
            await Assert.That(await UserManager.GetAuthenticatorKeyAsync(after)).IsNotEqualTo(keyBefore);
            await Assert.That(await UserManager.CountRecoveryCodesAsync(after)).IsEqualTo(0);
            await _securityEmails.Received(1).TwoFactorOffAsync(Arg.Is<FinanceTrackerUser>(x => x.Id == Id(Sam)),
                TwoFactorOffCause.TurnedOffByAdmin);
        }
    }

    [Test]
    public async Task TurnOffTwoFactorAsync_Yourself_IsRefusedAndChangesNothing()
    {
        // Arrange
        FinanceTrackerUser alex = (await UserManager.FindByIdAsync(Id(Alex).ToString()))!;
        await UserManager.SetTwoFactorEnabledAsync(alex, true);

        // Act
        bool turnedOff = await Subject().TurnOffTwoFactorAsync(Id(Alex), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(turnedOff).IsFalse();
            await Assert.That((await FindAsync(Alex)).TwoFactorEnabled).IsTrue();
            await _securityEmails.DidNotReceiveWithAnyArgs().TwoFactorOffAsync(default!, default);
        }
    }

    [Test]
    public async Task TurnOffTwoFactorAsync_SomeoneWithItOff_ClearsAnyKeyWithoutEmailing()
    {
        // Act
        bool turnedOff = await Subject().TurnOffTwoFactorAsync(Id(Sam), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(turnedOff).IsTrue();
            await Assert.That((await FindAsync(Sam)).TwoFactorEnabled).IsFalse();
            await _securityEmails.DidNotReceiveWithAnyArgs().TwoFactorOffAsync(default!, default);
        }
    }

    [Test]
    public async Task GetUsersAsync_SomeoneWithTwoFactorOn_SaysSo()
    {
        // Arrange
        FinanceTrackerUser sam = (await UserManager.FindByIdAsync(Id(Sam).ToString()))!;
        await UserManager.SetTwoFactorEnabledAsync(sam, true);

        // Act
        List<UserSummary> users = await Subject().GetUsersAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(users.Single(x => x.Username == Sam).TwoFactorEnabled).IsTrue();
            await Assert.That(users.Single(x => x.Username == Alex).TwoFactorEnabled).IsFalse();
        }
    }

    /// <summary>
    /// The service as Alex uses it.
    /// </summary>
    /// <param name="passwordSignIn">Whether password sign in is on.</param>
    private UsersService Subject(bool passwordSignIn = true) =>
        new(UserManager, Substitute.For<IPasswordReset>(), CurrentUserFor(Alex),
            new SignInMethods { Password = passwordSignIn, SingleSignOn = true }, _securityEmails,
            NullLogger<UsersService>.Instance);

    private Guid Id(string username) => Scenario.UserMappings[username];

    /// <summary>
    /// The user as saved now, rather than as an earlier lookup tracked them.
    /// </summary>
    private async Task<FinanceTrackerUser> FindAsync(string username)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return (await context.Users.FindAsync([Id(username)], CancellationToken))!;
    }
}
