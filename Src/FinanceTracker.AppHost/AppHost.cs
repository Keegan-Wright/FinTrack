using Microsoft.Extensions.Configuration;
#pragma warning disable ASPIRECOMPUTE003

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var registry = builder.AddContainerRegistry(
    "docker-hub",                              // Registry name
    "docker.io",                           // Registry endpoint
   "keeganwright12"     // Repository path
);

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
    .WithDataVolume(isReadOnly: false)
    .WithPersistence(TimeSpan.FromMinutes(5));


IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("FinTrack-Postgres")
    .WithPgWeb()
    .WithDataVolume(isReadOnly: false);


IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase("FinTrackDb");

#pragma warning disable ASPIREPIPELINES003
builder.AddProject<Projects.FinanceTracker>("FinTrack")

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
    .WaitFor(postgresDb);

await builder.Build().RunAsync();
