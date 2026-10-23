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


IResourceBuilder<ParameterResource> encryptionKeys =
    builder.AddParameterFromConfiguration("FinTrack-Encryption-Keys", "Encryption:Keys", secret: true);
IResourceBuilder<ParameterResource> encryptionCurrentKeyId =
    builder.AddParameterFromConfiguration("FinTrack-Encryption-Current-Key-Id", "Encryption:CurrentKeyId");

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



IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres(FinTrackResources.Postgres)
    .WithDataVolume(isReadOnly: false)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Restart = "unless-stopped";
    });


IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase(FinTrackResources.Database);


IResourceBuilder<ProjectResource> finTrack = builder.AddProject<Projects.FinanceTracker>(FinTrackResources.Web)

    .WithEnvironment(FinTrackEnvironment.EncryptionKeys, encryptionKeys)
    .WithEnvironment(FinTrackEnvironment.EncryptionCurrentKeyId, encryptionCurrentKeyId)
    .WithEnvironment(FinTrackEnvironment.TrueLayerBaseAuthUrl, openBankingAuthUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerBaseDataUrl, openBankingDataUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerAuthRedirectUrl, openBankingAuthRedirectUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerClientId, openBankingClientId)
    .WithEnvironment(FinTrackEnvironment.TrueLayerClientSecret, openBankingClientSecret)
    .WithEnvironment(FinTrackEnvironment.PublicIpAddress, openBankingPublicIpAddress)
    .WithEnvironment(FinTrackEnvironment.Culture, "en-GB")
    .WithReference(postgresDb)
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
