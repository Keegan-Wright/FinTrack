namespace FinanceTracker.Configurations;

public sealed class TrueLayerOpenBankingConfiguration
{
    public required Uri BaseAuthUrl { get; init; }
    public required Uri BaseDataUrl { get; init; }
    public required Uri AuthRedirectUrl { get; init; }
    public required string ClientId { get; init; }
    public required Guid ClientSecret { get; init; }
}
