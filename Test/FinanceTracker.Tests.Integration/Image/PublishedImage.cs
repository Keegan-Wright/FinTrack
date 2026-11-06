namespace FinanceTracker.Tests.Integration.Image;

/// <summary>
/// The published Docker image, running somewhere for <see cref="PublishedImageTests"/> in CI the image just built,
/// started with a Postgres, or locally the Docker Compose one.
/// </summary>
public static class PublishedImage
{
    /// <summary>
    /// The environment variable with the running image's address.
    /// </summary>
    public const string AddressVariable = "FINTRACK_IMAGE_URL";

    /// <summary>
    /// Where the image runs, null when not set.
    /// </summary>
    public static Uri? Address { get; } = AddressFromEnvironment();

    private static Uri? AddressFromEnvironment()
    {
        string? value = Environment.GetEnvironmentVariable(AddressVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new Uri(value.EndsWith('/') ? value : value + "/");
    }
}
