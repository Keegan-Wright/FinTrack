namespace FinanceTracker.Api;

/// <summary>
/// Marks an endpoint whose answer <see cref="NoneAnswerFilter"/> turns into the response, so the OpenAPI document
/// can say 204 rather than a boolean for a true or false answer.
/// </summary>
internal sealed record NoneAnswerMetadata;
