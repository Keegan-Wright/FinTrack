namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

/// <summary>
/// The app's code saves changes through tracked entities only. A bulk <c>ExecuteUpdate</c> writes straight to the
/// database, skipping the save interceptors, so the rows it changes would keep their old integrity tags and be refused
/// on their next load, see <c>RowIntegrity</c>. Deletes are fine, a deleted row is never loaded.
/// </summary>
public class BulkUpdateTests
{
    [Test]
    public async Task Src_NeverUsesExecuteUpdate()
    {
        // Arrange
        string src = Path.Combine(RepositoryRoot(), "Src");

        // Act
        List<string> files = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(static x => !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .ToList();
        List<string> offenders = files
            .Where(static x => File.ReadAllText(x).Contains("ExecuteUpdate", StringComparison.Ordinal))
            .Select(x => Path.GetRelativePath(src, x))
            .ToList();

        // Assert, having looked at the code at all
        using (Assert.Multiple())
        {
            await Assert.That(files.Count).IsGreaterThan(100);
            await Assert.That(offenders).IsEmpty();
        }
    }

    // The folder holding the solution, found by walking up from the tests' build output
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FinanceTracker.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Couldn't find the repository root.");
    }
}
