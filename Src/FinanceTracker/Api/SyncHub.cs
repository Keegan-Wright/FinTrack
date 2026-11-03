using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker.Api;

/// <summary>
/// Pushes <see cref="SyncHubContract.SyncComplete"/> to a user's browsers when a sync of their connections finishes,
/// and <see cref="SyncHubContract.JobChanged"/> to admins' browsers when a background job starts or finishes.
/// Server to client only, <see cref="SyncNotifier"/> sends through it. Connections are grouped by the signed in
/// user's id, SignalR's default user identifier, and an admin's also join <see cref="AdminsGroup"/>.
/// </summary>
[Authorize]
public sealed class SyncHub : Hub
{
    /// <summary>
    /// Every admin's connections, who hear about the background jobs.
    /// </summary>
    public const string AdminsGroup = "admins";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(FinTrackRoles.Admin) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        }

        await base.OnConnectedAsync();
    }
}
