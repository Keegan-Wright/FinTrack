using System.Net;
using System.Text;
using FinanceTracker.Client.Api;
using FinanceTracker.Contracts;

namespace FinanceTracker.Tests.Unit.Client.Api;

public class ApiHttpClientExtensionsTests
{
    private static readonly Uri Path = new("https://localhost/api/sharing/links/1/accounts");

    [Test]
    public async Task EnsureFoundAsync_RuleRefusedIt_ThrowsWithTheRulesMessage()
    {
        // Arrange, as the API's ExpectedFailureFilter answers a broken rule
        using HttpResponseMessage response = Answer(HttpStatusCode.UnprocessableEntity,
            """{"status":422,"detail":"Only your own accounts can be shared."}""");

        // Act
        RuleBrokenException? thrown = await Assert.ThrowsAsync<RuleBrokenException>(() =>
            response.EnsureFoundAsync(CancellationToken.None));

        // Assert
        await Assert.That(thrown?.Message).IsEqualTo("Only your own accounts can be shared.");
    }

    [Test]
    [Arguments(null)]
    [Arguments("not json")]
    [Arguments("""{"status":422}""")]
    public async Task EnsureFoundAsync_RuleRefusedItWithoutSayingWhy_ThrowsWithAGeneralMessage(string? body)
    {
        // Arrange
        using HttpResponseMessage response = Answer(HttpStatusCode.UnprocessableEntity, body);

        // Act
        RuleBrokenException? thrown = await Assert.ThrowsAsync<RuleBrokenException>(() =>
            response.EnsureFoundAsync(CancellationToken.None));

        // Assert
        await Assert.That(thrown?.Message).IsEqualTo($"{Path} was refused by one of the rules.");
    }

    [Test]
    public async Task EnsureFoundAsync_NotFound_ThrowsNotFound()
    {
        // Arrange
        using HttpResponseMessage response = Answer(HttpStatusCode.NotFound, null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => response.EnsureFoundAsync(CancellationToken.None));
    }

    [Test]
    public async Task EnsureFoundAsync_Succeeded_DoesNotThrow()
    {
        // Arrange
        using HttpResponseMessage response = Answer(HttpStatusCode.OK, null);

        // Act & Assert
        await Assert.That(() => response.EnsureFoundAsync(CancellationToken.None)).ThrowsNothing();
    }

    [Test]
    public async Task EnsureNameFreeAsync_RuleRefusedIt_ThrowsWithTheRulesMessage()
    {
        // Arrange
        using HttpResponseMessage response = Answer(HttpStatusCode.UnprocessableEntity,
            """{"detail":"A savings goal can't be moved to a card."}""");

        // Act
        RuleBrokenException? thrown = await Assert.ThrowsAsync<RuleBrokenException>(() =>
            response.EnsureNameFreeAsync("savings goal", "Holiday", CancellationToken.None));

        // Assert
        await Assert.That(thrown?.Message).IsEqualTo("A savings goal can't be moved to a card.");
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string? body)
    {
        HttpResponseMessage response = new(status) { RequestMessage = new HttpRequestMessage(HttpMethod.Put, Path) };
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, "application/problem+json");
        }

        return response;
    }
}
