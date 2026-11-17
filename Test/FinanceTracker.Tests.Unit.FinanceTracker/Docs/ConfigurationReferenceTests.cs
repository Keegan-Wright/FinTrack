using System.Reflection;
using FinanceTracker.AppHost.ServiceDefaults;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Docs;

/// <summary>
/// The self-hosting configuration reference lists every setting the app reads, so a new one can't go undocumented.
/// </summary>
public class ConfigurationReferenceTests
{
    private const string ReferencePath = "Docs/self-hosting/configuration.md";

    /// <summary>
    /// Settings only the integration tests set, which nobody self-hosting should.
    /// </summary>
    private static readonly string[] TestOnlySettings =
        [FinTrackEnvironment.TestClock, FinTrackEnvironment.OidcAllowHttp];

    [Test]
    public async Task EverySetting_IsInTheReference()
    {
        // Arrange
        string reference = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), ReferencePath));

        // Act
        List<string> missing = Settings()
            .Except(TestOnlySettings)
            .Where(x => !reference.Contains($"`{x}`", StringComparison.Ordinal))
            .ToList();

        // Assert
        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task TestOnlySettings_AreLeftOut()
    {
        // Arrange
        string reference = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), ReferencePath));

        // Act
        List<string> listed = TestOnlySettings
            .Where(x => reference.Contains($"`{x}`", StringComparison.Ordinal))
            .ToList();

        // Assert
        await Assert.That(listed).IsEmpty();
    }

    /// <summary>
    /// Every setting's name, from <see cref="FinTrackEnvironment"/>'s constants.
    /// </summary>
    private static IEnumerable<string> Settings() =>
        typeof(FinTrackEnvironment)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static x => x.IsLiteral && x.FieldType == typeof(string))
            .Select(static x => (string)x.GetRawConstantValue()!);

    /// <summary>
    /// The repository's root, the first folder above the tests with the solution in it.
    /// </summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FinanceTracker.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("The repository root wasn't found.");
    }
}
