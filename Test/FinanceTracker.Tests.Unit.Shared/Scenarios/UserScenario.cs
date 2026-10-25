using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds users as Identity's <see cref="UserManager{TUser}"/> stores them, normalised names and a security stamp
/// included, so its lookups find them.
/// </summary>
public sealed class UserScenario : ScenarioBuilder
{
    private static readonly UpperInvariantLookupNormalizer Normalizer = new();

    public UserScenario(TestingScenario scenario) : base(scenario)
    {
    }

    public Task<TestingScenario> WithUser(string firstName, string lastName, string email, string username) =>
        WithUser(new FinanceTrackerUser(firstName, lastName, email, username));

    /// <summary>
    /// Adds a user with generated details, mapped by <paramref name="username"/>.
    /// </summary>
    public Task<TestingScenario> WithUser(string username) =>
        WithUser(new FinanceTrackerUserFaker(username).Generate());

    public Task<TestingScenario> WithEmail(string username, string email) =>
        PersistAsync(async context =>
        {
            FinanceTrackerUser user = await LoadAsync(context, username);
            user.Email = email;
            user.NormalizedEmail = Normalizer.NormalizeEmail(email);
        });

    /// <summary>
    /// Links the user to an OpenID Connect provider's <paramref name="subject"/>.
    /// </summary>
    public Task<TestingScenario> WithOidcSubject(string username, string subject) =>
        PersistAsync(async context => (await LoadAsync(context, username)).SetOidcSubject(subject));

    /// <summary>
    /// Puts the user in the <paramref name="role"/>, adding the role when it doesn't exist yet.
    /// </summary>
    public Task<TestingScenario> WithRole(string username, string role) =>
        PersistAsync(async context =>
        {
            string normalizedRole = Normalizer.NormalizeName(role);
            FinanceTrackerRole? existing = await context.Roles
                .SingleOrDefaultAsync(x => x.NormalizedName == normalizedRole, Scenario.CancellationToken);

            if (existing is null)
            {
                existing = new FinanceTrackerRole { Id = Guid.NewGuid(), Name = role, NormalizedName = normalizedRole };
                context.Roles.Add(existing);
            }

            context.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = Scenario.UserMappings[username],
                RoleId = existing.Id
            });
        });

    private Task<TestingScenario> WithUser(FinanceTrackerUser user)
    {
        user.Id = Guid.NewGuid();
        user.NormalizedUserName = Normalizer.NormalizeName(user.UserName);
        user.NormalizedEmail = Normalizer.NormalizeEmail(user.Email);
        user.SecurityStamp = Guid.NewGuid().ToString();

        Scenario.UserMappings.Add(user.UserName!, user.Id);
        return PersistAsync(context => context.Users.Add(user));
    }

    private Task<FinanceTrackerUser> LoadAsync(FinanceTrackerContext context, string username)
    {
        Guid userId = Scenario.UserMappings[username];
        return context.Users.SingleAsync(x => x.Id == userId, Scenario.CancellationToken);
    }
}
