namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// The standard time limits for tests, so a wait that will never pass fails rather than hangs, and the same kind of
/// wait gets the same time everywhere. They're generous as they only run out when something's wrong, a full
/// parallel run on a busy machine shouldn't fail a test that passes.
/// </summary>
/// <remarks>
/// Linked into the client tests as a file, referencing this project would bring the server's code into them.
/// </remarks>
public static class TestTimeouts
{
    /// <summary>
    /// Something in the test's own process like a render settling, a dialog closing, a stubbed call arriving. bUnit's
    /// waits use it too, see the client tests' <c>ComponentTestBase</c>.
    /// </summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A background job the running app is asked to run.
    /// </summary>
    public static readonly TimeSpan Job = TimeSpan.FromSeconds(90);

    /// <summary>
    /// A whole integration test.
    /// </summary>
    public static readonly TimeSpan IntegrationTest = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The AppHost's resources starting, Postgres container included.
    /// </summary>
    public static readonly TimeSpan Startup = TimeSpan.FromMinutes(3);
}
