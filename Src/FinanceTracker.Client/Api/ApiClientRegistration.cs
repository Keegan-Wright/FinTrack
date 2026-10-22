using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Client.Api;

public static class ApiClientRegistration
{
    /// <summary>
    /// The named <see cref="HttpClient"/> every API client uses.
    /// </summary>
    public const string HttpClientName = "FinTrack.Api";

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the HTTP implementation of every <c>Contracts.Services</c> interface, so the pages run against
        /// the API instead of the server's services. For the front end once it runs in the browser, where the
        /// browser carries the sign in cookie.
        /// </summary>
        /// <param name="baseAddress">The app's address, ending with <c>/</c>.</param>
        /// <param name="configureSyncHub">
        /// Adjusts the sync hub connection, e.g. its cookies when not running in a browser.
        /// </param>
        /// <returns>The shared HTTP client's builder, to configure its handler.</returns>
        public IHttpClientBuilder AddFinTrackApiClients(Uri baseAddress,
            Action<HttpConnectionOptions>? configureSyncHub = null)
        {
            services.AddScoped<IAccountService, AccountApiClient>();
            services.AddScoped<IAutomationService, AutomationApiClient>();
            services.AddScoped<IBudgetCategoriesService, BudgetCategoriesApiClient>();
            services.AddScoped<ICalendarService, CalendarApiClient>();
            services.AddScoped<IClassificationService, ClassificationApiClient>();
            services.AddScoped<IDashboardService, DashboardApiClient>();
            services.AddScoped<IHouseholdMemberService, HouseholdMemberApiClient>();
            services.AddScoped<IOpenBankingService, OpenBankingApiClient>();
            services.AddScoped<IReportService, ReportApiClient>();
            services.AddScoped<ITransactionsService, TransactionsApiClient>();

            // One connection shared by every subscriber, the container disposes it
            services.AddSingleton(_ => new HubConnectionBuilder()
                .WithUrl(new Uri(baseAddress, SyncHubContract.Path), options => configureSyncHub?.Invoke(options))
                .WithAutomaticReconnect()
                .Build());
            services.AddSingleton<ISyncNotifications, HubSyncNotifications>();

            return services.AddHttpClient(HttpClientName, client =>
            {
                client.BaseAddress = baseAddress;
                client.DefaultRequestHeaders.Add(ApiHeaders.Request, "1");
            });
        }
    }
}
