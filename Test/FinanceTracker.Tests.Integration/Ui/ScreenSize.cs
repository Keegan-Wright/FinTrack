namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// A screen a page should work on, every page working on a phone, a tablet and a desktop.
/// </summary>
public sealed record ScreenSize(string Name, int Width, int Height)
{
    public static ScreenSize Phone { get; } = new("Phone", 390, 844);

    public static ScreenSize Tablet { get; } = new("Tablet", 820, 1180);

    public static ScreenSize Desktop { get; } = new("Desktop", 1440, 900);

    public static IReadOnlyList<ScreenSize> All { get; } = [Phone, Tablet, Desktop];

    /// <summary>
    /// Whether the navigation drawer sits over the page rather than docked beside it, below MudBlazor's large
    /// breakpoint, as the app's layout sets it.
    /// </summary>
    public bool DrawerOverPage => Width < 1280;

    public override string ToString() => Name;
}
