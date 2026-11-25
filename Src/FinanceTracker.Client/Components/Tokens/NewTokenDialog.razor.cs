namespace FinanceTracker.Client.Components.Tokens;

/// <summary>
/// What a new token is for, asked only when there's a choice, which only the AI build has.
/// </summary>
public partial class NewTokenDialog
{
#if FINTRACK_AI
    private const string ForApps = "apps";
    private const string ForAiClients = "ai";

    /// <summary>
    /// What a token can be for.
    /// </summary>
    private static IReadOnlyList<TokenUseOption> UseOptions { get; } =
    [
        new(ForApps, "Apps and scripts"),
        new(ForAiClients, "AI clients (MCP)")
    ];

    /// <summary>
    /// What this one is for.
    /// </summary>
    private string Use
    {
        get => _model.ForAiClients ? ForAiClients : ForApps;
        set => _model.ForAiClients = value == ForAiClients;
    }

    /// <summary>
    /// The line under the choice.
    /// </summary>
    private string UseHelperText => _model.ForAiClients
        ? "Only AI clients can use it, through MCP, never the API"
        : "Reads the API, AI clients can't use it";
#else
    /// <summary>
    /// What a token can be for, nothing to choose.
    /// </summary>
    private static IReadOnlyList<TokenUseOption> UseOptions { get; } = [];

    /// <summary>
    /// What this one is for, never asked.
    /// </summary>
    private static string Use
    {
        get => string.Empty;
        set => _ = value;
    }

    /// <summary>
    /// The line under the choice, never shown.
    /// </summary>
    private static string UseHelperText => string.Empty;
#endif
}
