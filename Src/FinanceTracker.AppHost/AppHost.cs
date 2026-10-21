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


IResourceBuilder<ParameterResource> encryptionKey =
    builder.AddParameterFromConfiguration("FinTrack-Symmetric-Key", "Encryption:SymmetricKey", secret: true);
IResourceBuilder<ParameterResource> encryptionSalt =
    builder.AddParameterFromConfiguration("FinTrack-Symmetric-Salt", "Encryption:SymmetricSalt", secret: true);
// Part of the key derivation, so it must stay matched to the data's key and salt
IResourceBuilder<ParameterResource> encryptionIterations =
    builder.AddParameterFromConfiguration("FinTrack-Symmetric-Key-Iterations", "Encryption:Iterations");

IResourceBuilder<ParameterResource> openBankingAuthUrl =
    builder.AddParameterFromConfiguration("OpenBanking-Auth-Url", "OpenBanking:TrueLayer:BaseAuthUrl");
IResourceBuilder<ParameterResource> openBankingDataUrl =
    builder.AddParameterFromConfiguration("OpenBanking-Data-Url", "OpenBanking:TrueLayer:BaseDataUrl");
IResourceBuilder<ParameterResource> openBankingAuthRedirectUrl =
    builder.AddParameterFromConfiguration("OpenBanking-Auth-Redirect-Url", "OpenBanking:TrueLayer:AuthRedirectUrl");
IResourceBuilder<ParameterResource> openBankingClientId =
    builder.AddParameterFromConfiguration("OpenBanking-Client-Id", "OpenBanking:TrueLayer:ClientId");
IResourceBuilder<ParameterResource> openBankingClientSecret =
    builder.AddParameterFromConfiguration("OpenBanking-Client-Secret", "OpenBanking:TrueLayer:ClientSecret", secret: true);
IResourceBuilder<ParameterResource> openBankingPublicIpAddress =
    builder.AddParameterFromConfiguration("OpenBanking-Public-IP-Address", "OpenBanking:TrueLayer:PublicIpAddress");



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

// OIDC sign in is optional, it's only wired up when an authority is configured
if (!string.IsNullOrEmpty(builder.Configuration["OIDC:Authority"]))
{
    finTrack
        .WithEnvironment(FinTrackEnvironment.OidcAuthority,
            builder.AddParameterFromConfiguration("Oidc-Authority", "OIDC:Authority"))
        .WithEnvironment(FinTrackEnvironment.OidcClientId,
            builder.AddParameterFromConfiguration("Oidc-ClientId", "OIDC:ClientId"))
        .WithEnvironment(FinTrackEnvironment.OidcClientSecret,
            builder.AddParameterFromConfiguration("Oidc-ClientSecret", "OIDC:ClientSecret", secret: true));
}


await builder.Build().RunAsync();
