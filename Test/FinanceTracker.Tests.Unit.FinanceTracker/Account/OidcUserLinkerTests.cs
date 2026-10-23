using System.Security.Claims;
using FinanceTracker.Components.Account;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Account;

public class OidcUserLinkerTests : InfrastructureTestBase
{
    private const string OidcSubject = "authelia-subject";
    private const string Email = "jane@test.local";

    private ServiceProvider _services = null!;
    private AsyncServiceScope _scope;

    private UserManager<FinanceTrackerUser> UserManager =>
        _scope.ServiceProvider.GetRequiredService<UserManager<FinanceTrackerUser>>();

    private OidcUserLinker Subject => _scope.ServiceProvider.GetRequiredService<OidcUserLinker>();

    [Before(Test)]
    public void SetupIdentity()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped(_ => DbContextFactory.CreateDbContext());
        services.AddIdentityCore<FinanceTrackerUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<FinanceTrackerRole>()
            .AddEntityFrameworkStores<FinanceTrackerContext>();
        services.AddScoped<AdminProvisioner>();
        services.AddScoped<OidcUserLinker>();

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
    public async Task FindOrCreateAsync_ForANewSubjectCreatesTheUser()
    {
        // Act
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal());

        // Assert
        await Assert.That(user).IsNotNull();
        IList<Claim> claims = await UserManager.GetClaimsAsync(user!);
        using (Assert.Multiple())
        {
            await Assert.That(user!.OpenIdConnectSubject).IsEqualTo(OidcSubject);
            await Assert.That(user.UserName).IsEqualTo("jane");
            await Assert.That(user.Email).IsEqualTo(Email);
            await Assert.That(user.FirstName).IsEqualTo("Jane");
            await Assert.That(user.LastName).IsEqualTo("Doe");
            await Assert.That(claims.Any(x => x is { Type: CustomClaims.FirstName, Value: "Jane" })).IsTrue();
            await Assert.That(await UserManager.IsInRoleAsync(user, FinTrackRoles.Admin)).IsTrue();
        }
    }

    [Test]
    public async Task FindOrCreateAsync_WhenThePreferredUsernameIsTaken_UsesTheEmail()
    {
        // Arrange
        await CreateUserAsync("jane", "other@test.local");

        // Act
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(user!.UserName).IsEqualTo(Email);
            await Assert.That(await UserManager.IsInRoleAsync(user, FinTrackRoles.Admin)).IsFalse();
        }
    }

    [Test]
    public async Task FindOrCreateAsync_ForALinkedSubjectReturnsItsUser()
    {
        // Arrange
        FinanceTrackerUser linked = await CreateUserAsync("jdoe", "changed@test.local", OidcSubject);

        // Act, the email may have changed at the provider since
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(user!.Id).IsEqualTo(linked.Id);
            await Assert.That(await UserManager.Users.CountAsync()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task FindOrCreateAsync_ForALocalUserWithTheEmail_LinksThem()
    {
        // Arrange
        FinanceTrackerUser local = await CreateUserAsync("jdoe", Email);

        // Act
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal());

        // Assert
        FinanceTrackerUser? stored = await UserManager.FindByIdAsync(local.Id.ToString());
        using (Assert.Multiple())
        {
            await Assert.That(user!.Id).IsEqualTo(local.Id);
            await Assert.That(stored!.OpenIdConnectSubject).IsEqualTo(OidcSubject);
        }
    }

    [Test]
    public async Task FindOrCreateAsync_WhenTheEmailsUserIsLinkedElsewhere_Rejects()
    {
        // Arrange
        await CreateUserAsync("jdoe", Email, "another-subject");

        // Act
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal());

        // Assert
        await Assert.That(user).IsNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("false")]
    public async Task FindOrCreateAsync_WithoutAVerifiedEmail_Rejects(string? emailVerified)
    {
        // Act
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal(emailVerified: emailVerified));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(user).IsNull();
            await Assert.That(await UserManager.Users.CountAsync()).IsEqualTo(0);
        }
    }

    [Test]
    public async Task FindOrCreateAsync_FromUserInfo_AcceptsItsBooleanText()
    {
        // Act
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal(emailVerified: "True"));

        // Assert
        await Assert.That(user).IsNotNull();
    }

    [Test]
    public async Task FindOrCreateAsync_WithoutASubjectRejects()
    {
        // Act
        FinanceTrackerUser? user = await Subject.FindOrCreateAsync(Principal(subject: null));

        // Assert
        await Assert.That(user).IsNull();
    }

    private static ClaimsPrincipal Principal(string? subject = OidcSubject, string? emailVerified = "true")
    {
        List<Claim> claims =
        [
            new(OidcUserLinker.Email, Email),
            new(OidcUserLinker.GivenName, "Jane"),
            new(OidcUserLinker.FamilyName, "Doe"),
            new(OidcUserLinker.PreferredUsername, "jane")
        ];
        if (subject is not null)
        {
            claims.Add(new Claim(OidcUserLinker.Subject, subject));
        }

        if (emailVerified is not null)
        {
            claims.Add(new Claim(OidcUserLinker.EmailVerified, emailVerified));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "oidc"));
    }

    private async Task<FinanceTrackerUser> CreateUserAsync(string userName, string email, string? subject = null)
    {
        FinanceTrackerUser user = subject is null
            ? new FinanceTrackerUser("Jane", "Doe", email, userName)
            : new FinanceTrackerUser("Jane", "Doe", email, userName, subject);
        IdentityResult result = await UserManager.CreateAsync(user);
        await Assert.That(result.Succeeded).IsTrue();
        return user;
    }
}
