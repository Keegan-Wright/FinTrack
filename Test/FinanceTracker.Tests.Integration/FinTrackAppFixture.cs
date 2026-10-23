using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using System.Net;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Integration.OpenBanking;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.EntityFrameworkCore;
using TUnit.Aspire;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Runs the AppHost once per test session. Postgres starts without a data volume, TrueLayer is
/// replaced with <see cref="FakeTrueLayer"/> and the app is given throwaway secrets
/// </summary>
public sealed class FinTrackAppFixture : AspireFixture<Projects.FinanceTracker_AppHost>
{

    public static readonly EncryptionConfiguration EncryptionConfiguration = TestEncryptionKeys.Keyring();

    public FakeTrueLayer TrueLayer { get; } = new();

    public IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; private set; } = null!;

    public Uri WebBaseAddress { get; private set; } = null!;

    /// <summary>
    /// The session's first registered user, so its admin. Registered before any test runs, which leaves every user a
    /// test registers a non-admin.
    /// </summary>
    public SignedInUser FirstUser { get; private set; } = null!;

    protected override string[] Args =>
    [
        $"--Encryption:Keys={TestEncryptionKeys.Serialise(EncryptionConfiguration)}",
        $"--Encryption:CurrentKeyId={EncryptionConfiguration.CurrentKeyId}",
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
        // Each run gets a fresh container rather than the dev data volume
        foreach (IResource resource in builder.Resources.Where(x => x.Name is FinTrackResources.Postgres))
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

        DbContextFactory = new NpgsqlDbContextFactory(options, new ColumnEncryptionService(EncryptionConfiguration));

        FirstUser = await RegisterFirstUserAsync();
    }

    private async Task<SignedInUser> RegisterFirstUserAsync()
    {
        string username = $"first-{Guid.NewGuid():N}";
        FinTrackClient client = new(WebBaseAddress);

        HttpResponseMessage response = await client.RegisterAsync(username, SignedInUser.DefaultPassword, CancellationToken.None);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Registering the first user returned {response.StatusCode}");
        }

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync();
        Guid userId = await context.Users.Where(x => x.UserName == username).Select(x => x.Id).SingleAsync();

        return new SignedInUser(username, userId, client);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        TrueLayer.Dispose();
    }
}
