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

    /// <param name="scenario">The scenario it seeds.</param>
    public UserScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a user with the details given, mapped by <paramref name="username"/>.
    /// </summary>
    /// <param name="firstName">Their first name.</param>
    /// <param name="lastName">Their last name.</param>
    /// <param name="email">Their email.</param>
    /// <param name="username">Their username, which also maps them.</param>
    public Task<TestingScenario> WithUser(string firstName, string lastName, string email, string username) =>
        WithUser(new FinanceTrackerUser(firstName, lastName, email, username));

    /// <summary>
    /// Adds a user with generated details, mapped by <paramref name="username"/>.
    /// </summary>
    /// <param name="username">Their username, which also maps them.</param>
    public Task<TestingScenario> WithUser(string username) =>
        WithUser(new FinanceTrackerUserFaker(username).Generate());

    /// <summary>
    /// Changes the user's email.
    /// </summary>
    /// <param name="username">The user, by their scenario name.</param>
    /// <param name="email">The new email.</param>
    public Task<TestingScenario> WithEmail(string username, string email) =>
        PersistAsync(async context =>
        {
            FinanceTrackerUser user = await LoadAsync(context, username);
            user.Email = email;
            user.NormalizedEmail = Normalizer.NormalizeEmail(email);
        });

    /// <summary>
    /// Makes the user's email unconfirmed, as it is from registering until they open the link emailed to it.
    /// </summary>
    /// <param name="username">The user, by their scenario name.</param>
    public Task<TestingScenario> WithUnconfirmedEmail(string username) =>
        PersistAsync(async context => (await LoadAsync(context, username)).EmailConfirmed = false);

    /// <summary>
    /// Locks the user as an admin does from the users page.
    /// </summary>
    /// <param name="username">The user, by their scenario name.</param>
    public Task<TestingScenario> WithLockedByAdmin(string username) =>
        PersistAsync(async context => (await LoadAsync(context, username)).LockByAdmin());

    /// <summary>
    /// Links the user to an OpenID Connect provider's <paramref name="subject"/>.
    /// </summary>
    /// <param name="username">The user, by their scenario name.</param>
    /// <param name="subject">Their id at the provider.</param>
    public Task<TestingScenario> WithOidcSubject(string username, string subject) =>
        PersistAsync(async context => (await LoadAsync(context, username)).SetOidcSubject(subject));

    /// <summary>
    /// Puts the user in the <paramref name="role"/>, adding the role when it doesn't exist yet.
    /// </summary>
    /// <param name="username">The user, by their scenario name.</param>
    /// <param name="role">The role's name.</param>
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

    /// <summary>
    /// Saves <paramref name="user"/> as Identity would, mapped by its username. Its email is confirmed.
    /// </summary>
    /// <param name="user">The user to save.</param>
    private Task<TestingScenario> WithUser(FinanceTrackerUser user)
    {
        user.Id = Guid.NewGuid();
        user.NormalizedUserName = Normalizer.NormalizeName(user.UserName);
        user.NormalizedEmail = Normalizer.NormalizeEmail(user.Email);
        user.SecurityStamp = Guid.NewGuid().ToString();

        // Confirmed, as an account is once its link is opened, so notifications are emailed; see WithUnconfirmedEmail
        user.EmailConfirmed = true;

        Scenario.UserMappings.Add(user.UserName!, user.Id);
        return PersistAsync(context => context.Users.Add(user));
    }

    /// <summary>
    /// Loads the user to change.
    /// </summary>
    /// <param name="context">The context to load it in.</param>
    /// <param name="username">The user, by their scenario name.</param>
    private Task<FinanceTrackerUser> LoadAsync(FinanceTrackerContext context, string username)
    {
        Guid userId = Scenario.UserMappings[username];
        return context.Users.SingleAsync(x => x.Id == userId, Scenario.CancellationToken);
    }
}
