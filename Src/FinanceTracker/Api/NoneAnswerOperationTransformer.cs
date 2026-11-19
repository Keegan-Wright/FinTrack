using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FinanceTracker.Api;

/// <summary>
/// Describes a true or false answer turned into a response by <see cref="NoneAnswerFilter"/> as the 204 it sends,
/// rather than the boolean the handler returns.
/// </summary>
internal sealed class NoneAnswerOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        bool answered = context.Description.ActionDescriptor.EndpointMetadata.OfType<NoneAnswerMetadata>().Any();
        if (!answered || operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        if (!operation.Responses.TryGetValue("200", out IOpenApiResponse? ok) || !IsBoolean(ok))
        {
            return Task.CompletedTask;
        }

        operation.Responses.Remove("200");
        operation.Responses["204"] = new OpenApiResponse { Description = "No Content" };
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether <paramref name="response"/> is a boolean.
    /// </summary>
    /// <param name="response">The response as described.</param>
    private static bool IsBoolean(IOpenApiResponse response) =>
        response.Content?.Values.Any(static x => x.Schema?.Type is { } type && type.HasFlag(JsonSchemaType.Boolean))
        == true;
}
