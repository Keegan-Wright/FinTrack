using System.Security.Claims;
using System.Text.Encodings.Web;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace FinanceTracker.Tokens;

/// <summary>
/// Signs in a request with a personal access token as the token's user. Checked on every request, so a revoked token
/// or a locked user stops working straight away. The principal has no roles, so admin endpoints are never reached, and
/// carries <see cref="PersonalAccessTokenDefaults.ScopeClaim"/> for <see cref="TokenAccessHandler"/>.
/// </summary>
public sealed class PersonalAccessTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IDbContextFactory<FinanceTrackerContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    /// <param name="options">The scheme's options.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="encoder">Encodes URLs.</param>
    /// <param name="contextFactory">Finds the token and its user.</param>
    /// <param name="timeProvider">The clock expiry and lockout are checked against.</param>
    public PersonalAccessTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, IDbContextFactory<FinanceTrackerContext> contextFactory, TimeProvider timeProvider)
        : base(options, logger, encoder)
    {
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = PersonalAccessTokenDefaults.TokenFrom(Request);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (!PersonalAccessTokenFormat.TryRead(token, out string? lookupId, out byte[]? secretHash))
        {
            return Refuse(null, "it isn't in FinTrack's token format");
        }

        await using FinanceTrackerContext context = await _contextFactory.CreateDbContextAsync(Context.RequestAborted);
        PersonalAccessToken? found = await context.PersonalAccessTokens
            .SingleOrDefaultAsync(x => x.LookupId == lookupId, Context.RequestAborted);
        if (found is null || !PersonalAccessTokenFormat.Matches(secretHash, found.SecretHash))
        {
            return Refuse(lookupId, "there's no such token");
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!found.IsActive(now))
        {
            return Refuse(lookupId, "it's expired or revoked");
        }

        FinanceTrackerUser? user = await context.Users.FindAsync([found.UserId], Context.RequestAborted);
        if (user is null || user.LockoutEnd > _timeProvider.GetUtcNow())
        {
            return Refuse(lookupId, "its user is locked");
        }

        await RecordUseAsync(context, found, now);
        return AuthenticateResult.Success(new AuthenticationTicket(PrincipalFor(user, found), Scheme.Name));
    }

    /// <summary>
    /// A refused token gets a plain 401 saying it's a bearer token that's wanted, never a redirect to sign in.
    /// </summary>
    /// <param name="properties">The challenge's properties.</param>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records the token being used, at most once a minute. Another request recording it at the same time is fine.
    /// </summary>
    private async Task RecordUseAsync(FinanceTrackerContext context, PersonalAccessToken token, DateTime now)
    {
        if (!token.RecordUse(now))
        {
            return;
        }

        try
        {
            await context.SaveChangesAsync(Context.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request with the same token saved it first
        }
    }

    /// <summary>
    /// The token's user as the request's, with what the token may do and no roles.
    /// </summary>
    private ClaimsPrincipal PrincipalFor(FinanceTrackerUser user, PersonalAccessToken token)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(PersonalAccessTokenDefaults.ScopeClaim, token.Scope.ToString()),
            new(PersonalAccessTokenDefaults.TokenIdClaim, token.Id.ToString())
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
    }

    /// <summary>
    /// Fails the sign in, logging why with the token's public part, never the token.
    /// </summary>
    private AuthenticateResult Refuse(string? lookupId, string reason)
    {
        Logger.LogTokenRefused(lookupId ?? "unreadable", reason);
        return AuthenticateResult.Fail($"The token was refused, {reason}.");
    }
}
