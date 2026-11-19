using FinanceTracker.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class DataProtectionExtensionsTests
{
    [Test]
    public async Task AddFinTrackDataProtection_NewKeys_UseAes256Gcm()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.AddFinTrackDataProtection();
        await using ServiceProvider services = builder.Services.BuildServiceProvider();

        // Act
        AlgorithmConfiguration? configuration = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value
            .AuthenticatedEncryptorConfiguration;

        // Assert
        await Assert.That((configuration as AuthenticatedEncryptorConfiguration)?.EncryptionAlgorithm)
            .IsEqualTo(EncryptionAlgorithm.AES_256_GCM);
    }
}
