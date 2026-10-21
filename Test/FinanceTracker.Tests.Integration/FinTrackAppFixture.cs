using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Tests.Integration.OpenBanking;
using Microsoft.EntityFrameworkCore;
using TUnit.Aspire;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Runs the AppHost once per test session. Postgres and Redis start without data volumes, TrueLayer is
/// replaced with <see cref="FakeTrueLayer"/> and the app is given throwaway secrets
/// </summary>
public sealed class FinTrackAppFixture : AspireFixture<Projects.FinanceTracker_AppHost>
{

    public static readonly EncryptionConfiguration EncryptionConfiguration = new()
    {
        SymmetricKey = "SymmetricKeyForIntegrationTesting",
        SymmetricSalt = "SymmetricSaltForIntegrationTesting",
        Iterations = 10
    };

    public FakeTrueLayer TrueLayer { get; } = new();

    public IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; private set; } = null!;

    public Uri WebBaseAddress { get; private set; } = null!;

    protected override string[] Args =>
    [
        $"--Encryption:SymmetricKey={EncryptionConfiguration.SymmetricKey}",
        $"--Encryption:SymmetricSalt={EncryptionConfiguration.SymmetricSalt}",
        $"--Encryption:Iterations={EncryptionConfiguration.Iterations}",
        $"--OpenBanking:TrueLayer:BaseAuthUrl={TrueLayer.AuthUrl}",
        $"--OpenBanking:TrueLayer:BaseDataUrl={TrueLayer.DataUrl}",
        $"--OpenBanking:TrueLayer:AuthRedirectUrl={TrueLayer.AuthUrl}redirect",
        $"--OpenBanking:TrueLayer:ClientId={FakeTrueLayer.ClientId}",
        $"--OpenBanking:TrueLayer:ClientSecret={FakeTrueLayer.ClientSecret}",
        "--OpenBanking:TrueLayer:PublicIpAddress=127.0.0.1",
        "--OIDC:Authority=",
        "--OIDC:ClientId=",
        "--OIDC:ClientSecret="
    ];

    protected override TimeSpan ResourceTimeout => TimeSpan.FromMinutes(3);

    protected override AspireFixtureOptions Options => new() { RetainResourceLogs = true };

    protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
    {
        // Each run gets fresh containers rather than the dev data volumes
        foreach (IResource resource in builder.Resources.Where(x => x.Name is FinTrackResources.Postgres or FinTrackResources.Redis))
        {
            foreach (ContainerMountAnnotation mount in resource.Annotations.OfType<ContainerMountAnnotation>().ToList())
            {
                resource.Annotations.Remove(mount);
            }
        }

        // The app only reports healthy once it has migrated and is serving requests
        ProjectResource web = builder.Resources.OfType<ProjectResource>().Single(x => x.Name == FinTrackResources.Web);
        builder.CreateResourceBuilder(web).WithHttpHealthCheck("/health");
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // The app redirects plain http to https, so tests talk to it over https directly
        WebBaseAddress = App.GetEndpoint(FinTrackResources.Web, "https");

        string? connectionString = await GetConnectionStringAsync(FinTrackResources.Database);

        DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseNpgsql(connectionString)
            .Options;

        DbContextFactory = new NpgsqlDbContextFactory(options, new SymmetricEncryptionService(EncryptionConfiguration));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        TrueLayer.Dispose();
    }

    private sealed class NpgsqlDbContextFactory(
        DbContextOptions<FinanceTrackerContext> options,
        ISymmetricEncryptionService symmetricEncryptionService) : IDbContextFactory<FinanceTrackerContext>
    {
        public FinanceTrackerContext CreateDbContext() => new(options, symmetricEncryptionService, TimeProvider.System);
    }
}
