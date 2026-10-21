using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using FinanceTracker.AppHost.ServiceDefaults;
using Microsoft.Extensions.Configuration;
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPIPELINES003

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);



IResourceBuilder<ContainerRegistryResource> registry = builder.AddContainerRegistry(
    "docker-hub",
    "docker.io",
    "keeganwright12"
);

builder.AddDockerComposeEnvironment("fintrack");

IConfigurationSection openBankingConfig = builder.Configuration.GetSection("OpenBanking");
IConfigurationSection encryptionConfig = builder.Configuration.GetSection("Encryption");
IConfigurationSection oidcConfig = builder.Configuration.GetSection("OIDC");

IResourceBuilder<ParameterResource> encryptionKey = builder.AddParameter("FinTrack-Symmetric-Key", encryptionConfig["SymmetricKey"] ?? string.Empty );
IResourceBuilder<ParameterResource> encryptionSalt = builder.AddParameter("FinTrack-Symmetric-Salt", encryptionConfig["SymmetricSalt"] ?? string.Empty);
IResourceBuilder<ParameterResource> encryptionIterations = builder.AddParameter("FinTrack-Symmetric-Key-Iterations",encryptionConfig["Iterations"] ?? string.Empty);

IResourceBuilder<ParameterResource> openBankingAuthUrl = builder.AddParameter("OpenBanking-Auth-Url", openBankingConfig["TrueLayer:BaseAuthUrl"] ?? string.Empty);
IResourceBuilder<ParameterResource> openBankingDataUrl = builder.AddParameter("OpenBanking-Data-Url", openBankingConfig["TrueLayer:BaseDataUrl"] ?? string.Empty);
IResourceBuilder<ParameterResource> openBankingAuthRedirectUrl = builder.AddParameter("OpenBanking-Auth-Redirect-Url", openBankingConfig["TrueLayer:AuthRedirectUrl"] ?? string.Empty);
IResourceBuilder<ParameterResource> openBankingClientId = builder.AddParameter("OpenBanking-Client-Id", openBankingConfig["TrueLayer:ClientId"] ?? string.Empty);
IResourceBuilder<ParameterResource> openBankingClientSecret = builder.AddParameter("OpenBanking-Client-Secret", openBankingConfig["TrueLayer:ClientSecret"] ?? string.Empty);
IResourceBuilder<ParameterResource> openBankingPublicIpAddress = builder.AddParameter("OpenBanking-Public-IP-Address", openBankingConfig["TrueLayer:PublicIpAddress"] ?? string.Empty);

IResourceBuilder<ParameterResource> oidcAuthority = builder.AddParameter("Oidc-Authority", oidcConfig["Authority"] ?? string.Empty);
IResourceBuilder<ParameterResource> oidcClientId = builder.AddParameter("Oidc-ClientId", oidcConfig["ClientId"] ?? string.Empty);
IResourceBuilder<ParameterResource> oidcClientSecret = builder.AddParameter("Oidc-ClientSecret", oidcConfig["ClientSecret"] ?? string.Empty);



IResourceBuilder<RedisResource> redis = builder.AddRedis(FinTrackResources.Redis)
    .WithDataVolume()
    .WithPersistence(TimeSpan.FromMinutes(5))
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Restart = "unless-stopped";
    });


IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres(FinTrackResources.Postgres)
    .WithDataVolume(isReadOnly: false)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Restart = "unless-stopped";
    });


IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase(FinTrackResources.Database);


IResourceBuilder<ProjectResource> finTrack = builder.AddProject<Projects.FinanceTracker>(FinTrackResources.Web)

    .WithEnvironment(FinTrackEnvironment.EncryptionKey, encryptionKey)
    .WithEnvironment(FinTrackEnvironment.EncryptionSalt, encryptionSalt)
    .WithEnvironment(FinTrackEnvironment.EncryptionIterations, encryptionIterations)
    .WithEnvironment(FinTrackEnvironment.TrueLayerBaseAuthUrl, openBankingAuthUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerBaseDataUrl, openBankingDataUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerAuthRedirectUrl, openBankingAuthRedirectUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerClientId, openBankingClientId)
    .WithEnvironment(FinTrackEnvironment.TrueLayerClientSecret, openBankingClientSecret)
    .WithEnvironment(FinTrackEnvironment.PublicIpAddress, openBankingPublicIpAddress)
    .WithEnvironment(FinTrackEnvironment.Culture, "en-GB")
    .WithEnvironment(FinTrackEnvironment.OidcAuthority, oidcAuthority)
    .WithEnvironment(FinTrackEnvironment.OidcClientId, oidcClientId)
    .WithEnvironment(FinTrackEnvironment.OidcClientSecret, oidcClientSecret)
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
