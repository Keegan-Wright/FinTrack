using FinanceTracker.AppHost;
using Microsoft.Extensions.Configuration;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IConfigurationSection sentryConfig = builder.Configuration.GetSection("Sentry");
IConfigurationSection openBankingConfig = builder.Configuration.GetSection("OpenBanking");
IConfigurationSection authConfig = builder.Configuration.GetSection("Auth");
IConfigurationSection encryptionConfig = builder.Configuration.GetSection("Encryption");

IResourceBuilder<RedisResource> redis = builder.AddRedis("financeTrackerRedis")
    .WithDataVolume(isReadOnly: false)
    .WithPersistence(TimeSpan.FromMinutes(5));

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("financeTrackerPostgres")
    .WithPgWeb()
    .WithDataVolume(isReadOnly: false);

IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase("financeTrackerPostgresDb");

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
