using FinanceTracker.Components.Account;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Tokens;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Tokens;
using FinanceTracker.Infrastructure.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tokens;

/// <summary>
/// <see cref="ITokensService"/> for the signed in user. Only the hash of a token's secret is kept, so the token is
/// shown the once it's made.
/// </summary>
public sealed class TokensService : ITokensService
{
    private readonly IDbContextFactory<FinanceTrackerContext> _contextFactory;
    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly SignInMethods _signInMethods;
    private readonly ISecurityEmails _securityEmails;
    private readonly ISingleSignOnConfirmation _singleSignOnConfirmation;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokensService> _logger;

    /// <param name="contextFactory">Keeps the tokens.</param>
    /// <param name="userManager">Checks the user's password.</param>
    /// <param name="currentUser">The signed in user.</param>
    /// <param name="signInMethods">Whether passwords can be used.</param>
    /// <param name="securityEmails">Tells the user when their tokens change.</param>
    /// <param name="singleSignOnConfirmation">Whether a single sign-on user has just confirmed it's them.</param>
    /// <param name="timeProvider">The clock expiry is worked out from.</param>
    /// <param name="logger">Its logger.</param>
    public TokensService(IDbContextFactory<FinanceTrackerContext> contextFactory,
        UserManager<FinanceTrackerUser> userManager, ICurrentUser currentUser, SignInMethods signInMethods,
        ISecurityEmails securityEmails, ISingleSignOnConfirmation singleSignOnConfirmation, TimeProvider timeProvider,
        ILogger<TokensService> logger)
    {
        _singleSignOnConfirmation = singleSignOnConfirmation;
        _contextFactory = contextFactory;
        _userManager = userManager;
        _currentUser = currentUser;
        _signInMethods = signInMethods;
        _securityEmails = securityEmails;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TokensOverview> GetTokensAsync(CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        TokenConfirmation? confirmation = await ConfirmationForAsync(user);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        await using FinanceTrackerContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        List<PersonalAccessToken> tokens = await context.PersonalAccessTokens.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .OrderByDescending(static x => x.Created)
            .ToListAsync(cancellationToken);

        List<TokenSummary> summaries = tokens.Select(x => new TokenSummary
            {
                Id = x.Id,
                Name = x.Name,
                CreatedAt = x.Created,
                ExpiresAt = x.ExpiresAt,
                LastUsedAt = x.LastUsedAt,
                Expired = !x.IsActive(now),
#if FINTRACK_AI
                ForAiClients = x.Scope == TokenScope.Ai
#endif
            })
            .ToList();

        bool confirmed = confirmation == TokenConfirmation.SingleSignOn &&
                         await _singleSignOnConfirmation.IsConfirmedAsync(user);
        return new TokensOverview { Confirmation = confirmation, Confirmed = confirmed, Tokens = summaries };
    }

    /// <inheritdoc/>
    public async Task<TokenCreation?> CreateAsync(CreateToken request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        TokenConfirmation? confirmation = await ConfirmationForAsync(user);
        if (confirmation is null)
        {
            return null;
        }

        string? refusal = await RefusalAsync(user, confirmation.Value, request.Password);
        if (refusal is not null)
        {
            return new TokenCreationRefused([refusal]);
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        IssuedToken issued = PersonalAccessTokenFormat.Issue();
        PersonalAccessToken token = new(user.Id, request.Name, issued.LookupId, issued.SecretHash,
            ExpiresAt(request.Expiry, now), ScopeFor(request));

        await using (FinanceTrackerContext context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            context.PersonalAccessTokens.Add(token);
            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTokenCreated(user.Id, token.Id);
        await _securityEmails.TokenCreatedAsync(user, token.Name);
        return new TokenCreated(issued.Token);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        await using FinanceTrackerContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        PersonalAccessToken? token = await context.PersonalAccessTokens
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == user.Id && x.RevokedAt == null, cancellationToken);
        if (token is null)
        {
            return false;
        }

        token.Revoke(_timeProvider.GetUtcNow().UtcDateTime);
        await context.SaveChangesAsync(cancellationToken);
        _logger.LogTokenRevoked(user.Id, token.Id);
        await _securityEmails.TokenRevokedAsync(user, token.Name);
        return true;
    }

    /// <summary>
    /// What a new token may do, read the API unless the AI build was asked for one for AI clients.
    /// </summary>
    /// <param name="request">The new token's details.</param>
    private static TokenScope ScopeFor(CreateToken request)
    {
#if FINTRACK_AI
        return request.ForAiClients ? TokenScope.Ai : TokenScope.Read;
#else
        ArgumentNullException.ThrowIfNull(request);
        return TokenScope.Read;
#endif
    }

    /// <summary>
    /// When a token made at <paramref name="now"/> lasting <paramref name="expiry"/> stops working, null for never.
    /// </summary>
    /// <param name="expiry">How long it lasts.</param>
    /// <param name="now">The time now, in UTC.</param>
    private static DateTime? ExpiresAt(TokenExpiry expiry, DateTime now)
    {
        switch (expiry)
        {
            case TokenExpiry.ThirtyDays:
                return now.AddDays(30);
            case TokenExpiry.NinetyDays:
                return now.AddDays(90);
            case TokenExpiry.OneYear:
                return now.AddYears(1);
            case TokenExpiry.Never:
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(expiry), expiry, null);
        }
    }

    /// <summary>
    /// The signed in user.
    /// </summary>
    /// <exception cref="InvalidOperationException">They no longer exist.</exception>
    private async Task<FinanceTrackerUser> UserAsync() =>
        await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
        ?? throw new InvalidOperationException("The signed in user no longer exists");

    /// <summary>
    /// How <paramref name="user"/> confirms it's them, with their password when they can use one, see
    /// <see cref="SignInMethods.CanUsePassword"/>, otherwise through the identity provider when they sign in there.
    /// Null when they can't do either, so can't have tokens.
    /// </summary>
    /// <param name="user">The user.</param>
    private async Task<TokenConfirmation?> ConfirmationForAsync(FinanceTrackerUser user)
    {
        if (_signInMethods.CanUsePassword(await _userManager.HasPasswordAsync(user)))
        {
            return TokenConfirmation.Password;
        }

        if (_signInMethods.SingleSignOn && user.OpenIdConnectSubject is not null)
        {
            return TokenConfirmation.SingleSignOn;
        }

        return null;
    }

    /// <summary>
    /// Why <paramref name="user"/> hasn't confirmed it's them, null when they have. A single sign-on confirmation is
    /// used up here, so it makes one token.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="confirmation">How they confirm.</param>
    /// <param name="password">The password they gave, for a password user.</param>
    private async Task<string?> RefusalAsync(FinanceTrackerUser user, TokenConfirmation confirmation,
        string password)
    {
        if (confirmation == TokenConfirmation.SingleSignOn)
        {
            if (await _singleSignOnConfirmation.UseAsync(user))
            {
                return null;
            }

            return "Confirm it's you with single sign-on first, it lasts 5 minutes";
        }

        if (string.IsNullOrEmpty(password))
        {
            return "Enter your password";
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            return "Your password isn't right";
        }

        return null;
    }
}
