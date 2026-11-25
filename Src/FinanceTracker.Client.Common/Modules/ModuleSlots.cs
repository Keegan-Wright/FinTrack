namespace FinanceTracker.Client.Common.Modules;

/// <summary>
/// The places in the app a module built in can show its own components, see <see cref="ModuleSlotAttribute"/>.
/// </summary>
public static class ModuleSlots
{
    /// <summary>
    /// The app bar, beside the notifications bell.
    /// </summary>
    public const string AppBar = "AppBar";

    /// <summary>
    /// The menu, after the app's own pages.
    /// </summary>
    public const string NavMenu = "NavMenu";

    /// <summary>
    /// The menu's Settings group, which only admins see.
    /// </summary>
    public const string SettingsNavMenu = "SettingsNavMenu";

    /// <summary>
    /// Your profile, after the app's own sections.
    /// </summary>
    public const string Profile = "Profile";
}
