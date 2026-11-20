namespace FinanceTracker.Tokens;

/// <summary>
/// Which requests a personal access token may make, for <see cref="TokenAccessHandler"/> and the OpenAPI document.
/// </summary>
public static class TokenReads
{
    /// <summary>
    /// Whether a <paramref name="method"/> request to an endpoint with <paramref name="metadata"/> only reads one
    /// opened to tokens. That's a GET or HEAD to an endpoint marked
    /// <see cref="TokenAccessExtensions.ReadableWithToken"/>, or a POST also marked
    /// <see cref="TokenAccessExtensions.ReadsOnly"/>.
    /// </summary>
    /// <param name="method">The request's HTTP method.</param>
    /// <param name="metadata">The endpoint's metadata.</param>
    public static bool Allows(string method, IEnumerable<object> metadata)
    {
        ArgumentNullException.ThrowIfNull(method);
        List<object> all = [.. metadata];
        if (!all.OfType<TokenReadableMetadata>().Any())
        {
            return false;
        }

        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
        {
            return true;
        }

        return HttpMethods.IsPost(method) && all.OfType<ReadsOnlyMetadata>().Any();
    }
}
