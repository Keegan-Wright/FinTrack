using FinanceTracker.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker.Api;

/// <summary>
/// Pushes <see cref="SyncHubContract.SyncComplete"/> to a user's browsers when a sync of their connections finishes.
/// Server to client only, <see cref="SyncNotifier"/> sends through it. Connections are grouped by the signed in
/// user's id, SignalR's default user identifier.
/// </summary>
[Authorize]
public sealed class SyncHub : Hub;
