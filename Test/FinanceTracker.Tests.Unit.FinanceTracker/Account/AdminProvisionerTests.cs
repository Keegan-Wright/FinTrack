using FinanceTracker.Components.Account;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Account;

public class AdminProvisionerTests : InfrastructureTestBase
{
    private ServiceProvider _services = null!;
    private AsyncServiceScope _scope;

    private UserManager<FinanceTrackerUser> UserManager =>
        _scope.ServiceProvider.GetRequiredService<UserManager<FinanceTrackerUser>>();

    private AdminProvisioner Subject => _scope.ServiceProvider.GetRequiredService<AdminProvisioner>();

    [Before(Test)]
    public void SetupIdentity()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped(_ => DbContextFactory.CreateDbContext());
        services.AddIdentityCore<FinanceTrackerUser>()
            .AddRoles<FinanceTrackerRole>()
            .AddEntityFrameworkStores<FinanceTrackerContext>();
        services.AddScoped<AdminProvisioner>();

        _services = services.BuildServiceProvider();
        _scope = _services.CreateAsyncScope();
    }

    [After(Test)]
    public async Task CleanupIdentityAsync()
    {
        await _scope.DisposeAsync();
        await _services.DisposeAsync();
    }

    [Test]
    public async Task GrantIfFirstUserAsync_ForTheFirstUser_GrantsAdmin()
    {
        // Arrange
        FinanceTrackerUser first = await CreateUserAsync("first");

        // Act
        await Subject.GrantIfFirstUserAsync(first);

        // Assert
        await Assert.That(await UserManager.IsInRoleAsync(first, FinTrackRoles.Admin)).IsTrue();
    }

    [Test]
    public async Task GrantIfFirstUserAsync_ForALaterUser_DoesNotGrantAdmin()
    {
        // Arrange
        await CreateUserAsync("first");
        FinanceTrackerUser second = await CreateUserAsync("second");

        // Act
        await Subject.GrantIfFirstUserAsync(second);

        // Assert
        await Assert.That(await UserManager.IsInRoleAsync(second, FinTrackRoles.Admin)).IsFalse();
    }

    [Test]
    public async Task EnsureAdminAsync_WithASingleUserAndNoAdmin_GrantsThemAdmin()
    {
        // Arrange, a user from before the admin role existed
        FinanceTrackerUser existing = await CreateUserAsync("existing");

        // Act
        await Subject.EnsureAdminAsync();

        // Assert
        await Assert.That(await UserManager.IsInRoleAsync(existing, FinTrackRoles.Admin)).IsTrue();
    }

    [Test]
    public async Task EnsureAdminAsync_WithSeveralUsersAndNoAdmin_GrantsNobody()
    {
        // Arrange
        await CreateUserAsync("one");
        await CreateUserAsync("two");

        // Act
        await Subject.EnsureAdminAsync();

        // Assert
        await Assert.That(await UserManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).IsEmpty();
    }

    [Test]
    public async Task EnsureAdminAsync_WithAnAdmin_LeavesTheRoleAlone()
    {
        // Arrange
        FinanceTrackerUser admin = await CreateUserAsync("admin");
        await Subject.GrantIfFirstUserAsync(admin);
        await CreateUserAsync("other");

        // Act
        await Subject.EnsureAdminAsync();

        // Assert
        await Assert.That((await UserManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).Select(x => x.Id))
            .IsEquivalentTo([admin.Id]);
    }

    private async Task<FinanceTrackerUser> CreateUserAsync(string username)
    {
        FinanceTrackerUser user = new("Test", "User", $"{username}@fintrack.test", username);
        IdentityResult result = await UserManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Description)));
        }

        return user;
    }
}
