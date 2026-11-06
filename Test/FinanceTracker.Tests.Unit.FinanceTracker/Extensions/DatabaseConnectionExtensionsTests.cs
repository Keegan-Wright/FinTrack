using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class DatabaseConnectionExtensionsTests
{
    [Test]
    public async Task GetFinTrackDatabaseConnectionString_TurnsKerberosOffAndKeepsTheRest()
    {
        // Arrange
        IConfiguration configuration = ConfigurationWith("Host=db;Port=5432;Username=app;Password=secret;Database=FinTrackDb");

        // Act
        NpgsqlConnectionStringBuilder read = new(configuration.GetFinTrackDatabaseConnectionString());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(read.GssEncryptionMode).IsEqualTo(GssEncryptionMode.Disable);
            await Assert.That(read.Host).IsEqualTo("db");
            await Assert.That(read.Username).IsEqualTo("app");
            await Assert.That(read.Password).IsEqualTo("secret");
            await Assert.That(read.Database).IsEqualTo("FinTrackDb");
        }
    }

    [Test]
    public async Task GetFinTrackDatabaseConnectionString_NoneConfigured_SaysSo()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().Build();

        // Act, Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(configuration.GetFinTrackDatabaseConnectionString()));
    }

    private static IConfiguration ConfigurationWith(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{FinTrackResources.Database}"] = connectionString
            })
            .Build();
}
