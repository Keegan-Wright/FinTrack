using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Sets <see cref="CurrentUser"/> for an interactive circuit. Circuits get their own DI scope with no HttpContext,
/// so the user comes from the circuit's authentication state, and follows it if that changes (e.g. the revalidating
/// provider signs a deleted user out).
/// </summary>
/// <remarks>
/// The pattern from https://learn.microsoft.com/aspnet/core/blazor/security/additional-scenarios#circuit-handler-to-capture-users-for-custom-services
/// </remarks>
internal sealed class CurrentUserCircuitHandler : CircuitHandler, IDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly CurrentUser _currentUser;

    public CurrentUserCircuitHandler(AuthenticationStateProvider authenticationStateProvider, CurrentUser currentUser)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _currentUser = currentUser;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        AuthenticationState state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        _currentUser.Set(state.User);
    }

    public void Dispose() => _authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => _ = UpdateAsync(task);

    private async Task UpdateAsync(Task<AuthenticationState> task)
    {
        try
        {
            AuthenticationState state = await task;
            _currentUser.Set(state.User);
        }
        catch
        {
            // A failed authentication state leaves the circuit acting for nobody
            _currentUser.Set(principal: null);
        }
    }
}
