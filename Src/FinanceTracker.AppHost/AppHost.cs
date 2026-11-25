using Aspire.Hosting.Dotnet;
using FinanceTracker.AppHost.ServiceDefaults;
#if FINTRACK_AI
using FinanceTracker.AppHost.Ollama;
#endif
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
    builder.AddParameterFromConfiguration("OpenBanking-Client-Secret", "OpenBanking:TrueLayer:ClientSecret",
        secret: true);
IResourceBuilder<ParameterResource> openBankingPublicIpAddress =
    builder.AddParameterFromConfiguration("OpenBanking-Public-IP-Address", "OpenBanking:TrueLayer:PublicIpAddress");


// A fixed port while running locally, so database tools keep their connection between runs. Empty for a free port,
// e.g. for the tests
int? postgresPort = builder.ExecutionContext.IsRunMode ? builder.Configuration.GetValue<int?>("Postgres:Port") : null;
IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres(FinTrackResources.Postgres, port: postgresPort)
    .WithDataVolume(isReadOnly: false)
    .PublishAsDockerComposeService((resource, service) => { service.Restart = "unless-stopped"; });


IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase(FinTrackResources.Database);


IResourceBuilder<DotnetProjectResource> finTrack = builder.AddDotnetProject(FinTrackResources.Web ,"../FinanceTracker/")
    .WithEnvironment(FinTrackEnvironment.EncryptionKeys, encryptionKeys)
    .WithEnvironment(FinTrackEnvironment.EncryptionCurrentKeyId, encryptionCurrentKeyId)
    .WithEnvironment(FinTrackEnvironment.TrueLayerBaseAuthUrl, openBankingAuthUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerBaseDataUrl, openBankingDataUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerAuthRedirectUrl, openBankingAuthRedirectUrl)
    .WithEnvironment(FinTrackEnvironment.TrueLayerClientId, openBankingClientId)
    .WithEnvironment(FinTrackEnvironment.TrueLayerClientSecret, openBankingClientSecret)
    .WithEnvironment(FinTrackEnvironment.PublicIpAddress, openBankingPublicIpAddress)
    .WithEnvironment(FinTrackEnvironment.Culture, "en-GB")
    .WithEnvironment(FinTrackEnvironment.WebhookAllowPrivate, true.ToString())
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .PublishAsDockerComposeService((resource, service) => { service.Restart = "unless-stopped"; })
    .WithContainerRegistry(registry)
    .WithRemoteImageTag("v0.0.1");

// Emails go to a mail catcher while developing, see its page from the dashboard. Only when running, a published
// FinTrack is given its own mail server through the SMTP_* settings, or sends none
if (builder.ExecutionContext.IsRunMode)
{
    IResourceBuilder<MailPitContainerResource> mail = builder.AddMailPit(FinTrackResources.Mail);
    finTrack
        .WithEnvironment(FinTrackEnvironment.SmtpHost, mail.Resource.Host)
        .WithEnvironment(FinTrackEnvironment.SmtpPort, mail.Resource.Port)
        // Plain SMTP, the catcher runs on this machine
        .WithEnvironment(FinTrackEnvironment.SmtpSecurity, "None")
        .WithEnvironment(FinTrackEnvironment.SmtpFrom, "FinTrack <fintrack@localhost>")
        .WithEnvironment(FinTrackEnvironment.PublicUrl, finTrack.GetEndpoint("https"))
        .WaitFor(mail);
}

// OIDC sign in is optional, it's only wired up when an authority is configured
#if FINTRACK_AI
// Ollama for the AI module, on this machine's GPU when it has one
finTrack.WithOllama(builder);
#endif

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
