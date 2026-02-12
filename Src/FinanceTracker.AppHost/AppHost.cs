using FinanceTracker.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var sentryConfig = builder.Configuration.GetSection("Sentry");
var openBankingConfig = builder.Configuration.GetSection("OpenBanking");
var authConfig = builder.Configuration.GetSection("Auth");
var encryptionConfig = builder.Configuration.GetSection("Encryption");

var redis = builder.AddRedis("financeTrackerRedis")
    .WithDataVolume(isReadOnly: false);

var postgres = builder.AddPostgres("financeTrackerPostgres")
    .WithPgWeb()
    .WithDataVolume(isReadOnly: false);

var postgresDb = postgres.AddDatabase("financeTrackerPostgresDb");

builder.AddProject<Projects.FinanceTracker>("FinanceTracker")
    .AddSentry(sentryConfig)
    .AddAuth(authConfig)
    .AddEncryption(encryptionConfig)
    .AddOpenBanking(openBankingConfig)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(postgresDb)
    .WaitFor(postgresDb);

await builder.Build().RunAsync();