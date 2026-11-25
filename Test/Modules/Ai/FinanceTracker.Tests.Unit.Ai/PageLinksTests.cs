using FinanceTracker.Ai.Tools;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Tests.Unit.Ai;

public class PageLinksTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    public async Task For_NoPublicAddress_GivesThePath(string? publicUrl)
    {
        // Arrange
        PageLinks subject = Subject(publicUrl);

        // Act
        string link = subject.For("/Budgets");

        // Assert
        await Assert.That(link).IsEqualTo("/Budgets");
    }

    [Test]
    [Arguments("https://fintrack.example.test")]
    [Arguments("https://fintrack.example.test/")]
    public async Task For_PublicAddressSet_GivesTheFullAddress(string publicUrl)
    {
        // Arrange
        PageLinks subject = Subject(publicUrl);

        // Act
        string link = subject.For("/Budgets");

        // Assert
        await Assert.That(link).IsEqualTo("https://fintrack.example.test/Budgets");
    }

    private static PageLinks Subject(string? publicUrl)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [PageLinks.PublicUrlVariable] = publicUrl })
            .Build();
        return new PageLinks(configuration);
    }
}
