using FinanceTracker.Components.Account;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
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
        await Scenario.Do(s => s.UserScenario.WithUser("first"));
        FinanceTrackerUser first = await FindUserAsync("first");

        // Act
        await Subject.GrantIfFirstUserAsync(first);

        // Assert
        await Assert.That(await UserManager.IsInRoleAsync(first, FinTrackRoles.Admin)).IsTrue();
    }

    [Test]
    public async Task GrantIfFirstUserAsync_ForALaterUser_DoesNotGrantAdmin()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser("first"))
            .Do(s => s.UserScenario.WithUser("second"));
        FinanceTrackerUser second = await FindUserAsync("second");

        // Act
        await Subject.GrantIfFirstUserAsync(second);

        // Assert
        await Assert.That(await UserManager.IsInRoleAsync(second, FinTrackRoles.Admin)).IsFalse();
    }

    [Test]
    public async Task EnsureAdminAsync_WithASingleUserAndNoAdmin_GrantsThemAdmin()
    {
        // Arrange, a user from before the admin role existed
        await Scenario.Do(s => s.UserScenario.WithUser("existing"));

        // Act
        await Subject.EnsureAdminAsync();

        // Assert
        await Assert.That(await UserManager.IsInRoleAsync(await FindUserAsync("existing"), FinTrackRoles.Admin)).IsTrue();
    }

    [Test]
    public async Task EnsureAdminAsync_WithSeveralUsersAndNoAdmin_GrantsNobody()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser("one"))
            .Do(s => s.UserScenario.WithUser("two"));

        // Act
        await Subject.EnsureAdminAsync();

        // Assert
        await Assert.That(await UserManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).IsEmpty();
    }

    [Test]
    public async Task EnsureAdminAsync_WithAnAdmin_LeavesTheRoleAlone()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser("admin"))
            .Do(s => s.UserScenario.WithRole("admin", FinTrackRoles.Admin))
            .Do(s => s.UserScenario.WithUser("other"));

        // Act
        await Subject.EnsureAdminAsync();

        // Assert
        await Assert.That((await UserManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).Select(x => x.Id))
            .IsEquivalentTo([Scenario.UserMappings["admin"]]);
    }

    private async Task<FinanceTrackerUser> FindUserAsync(string username) =>
        (await UserManager.FindByIdAsync(Scenario.UserMappings[username].ToString()))!;
}
