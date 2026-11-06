namespace FinanceTracker.AppHost.ServiceDefaults;

/// <summary>
/// Environment variables the AppHost sets on the web app, and the web app reads back as configuration.
/// </summary>
public static class FinTrackEnvironment
{
    public const string Culture = "APP_CULTURE";

    /// <summary>
    /// The keyring, <c>id:base64key</c> pairs separated by <c>;</c>.
    /// </summary>
    public const string EncryptionKeys = "ENCRYPTION_KEYS";

    /// <summary>
    /// The keyring id new values are encrypted with.
    /// </summary>
    public const string EncryptionCurrentKeyId = "ENCRYPTION_CURRENT_KEY_ID";

    public const string TrueLayerBaseAuthUrl = "OPEN_BANKING_TRUELAYER_BASE_AUTH_URL";
    public const string TrueLayerBaseDataUrl = "OPEN_BANKING_TRUELAYER_BASE_DATA_URL";
    public const string TrueLayerAuthRedirectUrl = "OPEN_BANKING_TRUELAYER_AUTH_REDIRECT_URL";
    public const string TrueLayerClientId = "OPEN_BANKING_TRUELAYER_CLIENT_ID";
    public const string TrueLayerClientSecret = "OPEN_BANKING_TRUELAYER_CLIENT_SECRET";
    public const string PublicIpAddress = "OPEN_BANKING_PUBLIC_IP_ADDRESS";

    /// <summary>
    /// <c>true</c> to let a request move the app's clock with the <see cref="TestClock.Cookie"/> cookie, so the
    /// integration tests can see what a day like the 1st of a month or a pay day looks like. Only the integration
    /// tests set it, never the AppHost.
    /// </summary>
    public const string TestClock = "FINTRACK_TEST_CLOCK";

    public const string OidcAuthority = "OIDC_Authority";
    public const string OidcClientId = "OIDC_ClientId";
    public const string OidcClientSecret = "OIDC_ClientSecret";
}
