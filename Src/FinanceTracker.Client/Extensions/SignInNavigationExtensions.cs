using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Client.Extensions;

public static class SignInNavigationExtensions
{
    extension(NavigationManager navigation)
    {
        /// <summary>
        /// Leaves the app for the server's login page, which brings the user back to where they are now once they've
        /// signed in.
        /// </summary>
        public void NavigateToSignIn() =>
            navigation.NavigateTo($"Account/Login?returnUrl={Uri.EscapeDataString(navigation.Uri)}", true);
    }
}
