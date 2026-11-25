namespace FinanceTracker.Client.Common.Modules;

/// <summary>
/// Shows a module's component in one of the app's places, once the module has loaded. The app finds it by this, as it
/// can't refer to a module's types.
/// </summary>
/// <param name="slot">Where it shows, one of <see cref="ModuleSlots"/>.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModuleSlotAttribute(string slot) : Attribute
{
    /// <summary>
    /// Where it shows, one of <see cref="ModuleSlots"/>.
    /// </summary>
    public string Slot { get; } = slot;
}
