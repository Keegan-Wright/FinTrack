using FinanceTracker.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var sentryConfig = builder.Configuration.GetSection("Sentry");
var openBankingConfig = builder.Configuration.GetSection("OpenBanking");
var authConfig = builder.Configuration.GetSection("Auth");

var postgres = builder.AddPostgres("financeTrackerPostgres")
    .WithDataVolume(isReadOnly: false);

var postgresDb = postgres.AddDatabase("financeTrackerPostgresDb");

builder.AddProject<Projects.FinanceTracker>("FinanceTracker")
    .AddSentry(sentryConfig)
    .AddAuth(authConfig)
    .AddOpenBanking(openBankingConfig)
    .WithReference(postgresDb)
    .WaitFor(postgresDb);

await builder.Build().RunAsync();