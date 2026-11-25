namespace FinanceTracker.Ai.Persistence;

/// <summary>
/// One of the AI module's app-wide settings, as the admin set it.
/// </summary>
public class AiSetting
{
    /// <summary>
    /// The setting's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Its value.
    /// </summary>
    public required string Value { get; set; }
}
