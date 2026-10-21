using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;

namespace FinanceTracker.Extensions;

public static class OpenBankingExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// TrueLayer's settings and the HTTP client its API calls use.
        /// </summary>
        public WebApplicationBuilder AddOpenBanking()
        {
            TrueLayerOpenBankingConfiguration trueLayerConfig = new()
            {
                BaseAuthUrl = builder.Configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerBaseAuthUrl)!,
                BaseDataUrl = builder.Configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerBaseDataUrl)!,
                AuthRedirectUrl = builder.Configuration.GetValue<Uri>(FinTrackEnvironment.TrueLayerAuthRedirectUrl)!,
                ClientId = builder.Configuration.GetValue<string>(FinTrackEnvironment.TrueLayerClientId)!,
                ClientSecret = builder.Configuration.GetValue<Guid>(FinTrackEnvironment.TrueLayerClientSecret),
                PublicIpAddress = builder.Configuration.GetValue<string>(FinTrackEnvironment.PublicIpAddress) ?? string.Empty
            };

            builder.Services.AddSingleton(trueLayerConfig);
            builder.Services.AddHttpClient(TrueLayerOpenBankingApiService.HttpClientName);

            return builder;
        }
    }
}
