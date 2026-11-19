using FinanceTracker.Components.Account;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Account;

/// <summary>
/// An admin, Alex, giving and taking the admin role and locking people, over Identity's stores.
/// </summary>
public class UsersServiceTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

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

    /// <summary>
    /// The service as Alex uses it.
    /// </summary>
    /// <param name="passwordSignIn">Whether password sign in is on.</param>
    private UsersService Subject(bool passwordSignIn = true) =>
        new(UserManager, Substitute.For<IPasswordReset>(), CurrentUserFor(Alex),
            new SignInMethods { Password = passwordSignIn, SingleSignOn = true }, NullLogger<UsersService>.Instance);

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
