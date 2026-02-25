using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Microsoft.Extensions.Configuration;
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPIPELINES003

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);



var registry = builder.AddContainerRegistry(
    "docker-hub",
    "docker.io",
    "keeganwright12"
);

builder.AddDockerComposeEnvironment("fintrack");

IConfigurationSection openBankingConfig = builder.Configuration.GetSection("OpenBanking");
IConfigurationSection encryptionConfig = builder.Configuration.GetSection("Encryption");

var encryptionKey = builder.AddParameter("FinTrack-Symmetric-Key", encryptionConfig["SymmetricKey"] ?? string.Empty );
var encryptionSalt = builder.AddParameter("FinTrack-Symmetric-Salt", encryptionConfig["SymmetricSalt"] ?? string.Empty);
var encryptionIterations = builder.AddParameter("FinTrack-Symmetric-Key-Iterations",encryptionConfig["Iterations"] ?? string.Empty);

var openBankingAuthUrl = builder.AddParameter("OpenBanking-Auth-Url", openBankingConfig["TrueLayer:BaseAuthUrl"] ?? string.Empty);
var openBankingDataUrl = builder.AddParameter("OpenBanking-Data-Url", openBankingConfig["TrueLayer:BaseDataUrl"] ?? string.Empty);
var openBankingAuthRedirectUrl = builder.AddParameter("OpenBanking-Auth-Redirect-Url", openBankingConfig["TrueLayer:AuthRedirectUrl"] ?? string.Empty);
var openBankingClientId = builder.AddParameter("OpenBanking-Client-Id", openBankingConfig["TrueLayer:ClientId"] ?? string.Empty);
var openBankingClientSecret = builder.AddParameter("OpenBanking-Client-Secret", openBankingConfig["TrueLayer:ClientSecret"] ?? string.Empty);


IResourceBuilder<RedisResource> redis = builder.AddRedis("FinTrack-Redis")
    .WithDataVolume()
    .WithPersistence(TimeSpan.FromMinutes(5))
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Restart = "unless-stopped";
    });


IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("FinTrack-Postgres")
    .WithPgWeb()
    .WithDataVolume(isReadOnly: false)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Restart = "unless-stopped";
    });


IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase("FinTrackDb");


var finTrack = builder.AddProject<Projects.FinanceTracker>("FinTrackWeb")

    .WithEnvironment("ENCRYPTION_KEY", encryptionKey)
    .WithEnvironment("ENCRYPTION_SALT", encryptionSalt)
    .WithEnvironment("ENCRYPTION_ITERATIONS", encryptionIterations)
    .WithEnvironment("OPEN_BANKING_TRUELAYER_BASE_AUTH_URL", openBankingAuthUrl)
    .WithEnvironment("OPEN_BANKING_TRUELAYER_BASE_DATA_URL", openBankingDataUrl)
    .WithEnvironment("OPEN_BANKING_TRUELAYER_AUTH_REDIRECT_URL", openBankingAuthRedirectUrl)
    .WithEnvironment("OPEN_BANKING_TRUELAYER_CLIENT_ID", openBankingClientId)
    .WithEnvironment("OPEN_BANKING_TRUELAYER_CLIENT_SECRET", openBankingClientSecret)
    .WithReference(redis)
    .WithReference(postgresDb)
    .WaitFor(redis)
    .WaitFor(postgresDb)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Restart = "unless-stopped";
    })
    .WithContainerRegistry(registry)
    .WithRemoteImageTag("v0.0.1");


await builder.Build().RunAsync();
