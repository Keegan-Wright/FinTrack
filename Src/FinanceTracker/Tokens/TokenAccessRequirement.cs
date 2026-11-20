using Microsoft.AspNetCore.Authorization;

namespace FinanceTracker.Tokens;

/// <summary>
/// A request signed in with a personal access token may only read the endpoints opened to tokens, see
/// <see cref="TokenAccessHandler"/>. Anyone else passes.
/// </summary>
public sealed class TokenAccessRequirement : IAuthorizationRequirement;
