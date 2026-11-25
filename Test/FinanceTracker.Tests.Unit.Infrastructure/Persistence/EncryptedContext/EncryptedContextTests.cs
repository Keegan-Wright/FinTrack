using FinanceTracker.Infrastructure.Common.Encryption;
using FinanceTracker.Infrastructure.Common.Persistence;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence.EncryptedContext;

/// <summary>
/// The shared encryption, blind indexes and row integrity work for any <see cref="IEncryptedContext"/>, not only the
/// core's, so a module's context gets the same protection.
/// </summary>
public sealed class EncryptedContextTests : UnitTestBase, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private readonly ColumnEncryptionService _firstKey = new(TestEncryptionKeys.Keyring(currentKeyId: 1, 1, 2));

    private readonly ColumnEncryptionService _secondKey = new(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2));

    [Before(Test)]
    public async Task CreateDatabaseAsync()
    {
        await _connection.OpenAsync();
        await using TestEncryptedContext context = Factory(_firstKey).CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task SaveChanges_EncryptsTheColumnAndReadsItBack()
    {
        // Act
        Guid id = await AddNoteAsync("Groceries");

        // Assert
        await using TestEncryptedContext context = Factory(_firstKey).CreateDbContext();
        TestNote note = await context.Notes.SingleAsync(x => x.Id == id, CancellationToken);
        string stored = await ReadRawTextAsync(id);
        using (Assert.Multiple())
        {
            await Assert.That(note.Text).IsEqualTo("Groceries");
            await Assert.That(stored).StartsWith("1:");
            await Assert.That(stored).DoesNotContain("Groceries");
        }
    }

    [Test]
    public async Task Load_RowChangedInTheDatabase_IsRefused()
    {
        // Arrange, one row's value copied over another's, as someone with database access might
        Guid first = await AddNoteAsync("Groceries");
        Guid second = await AddNoteAsync("Rent");
        string secondValue = await ReadRawTextAsync(second);
        await using (TestEncryptedContext context = Factory(_firstKey).CreateDbContext())
        {
            await context.Database.ExecuteSqlAsync(
                $"UPDATE \"Notes\" SET \"Text\" = {secondValue} WHERE \"Id\" = {first}", CancellationToken);
        }

        // Act
        await using TestEncryptedContext reading = Factory(_firstKey).CreateDbContext();
        Func<Task> load = async () => await reading.Notes.SingleAsync(x => x.Id == first, CancellationToken);

        // Assert
        await Assert.That(load).Throws<RowIntegrityException>();
    }

    [Test]
    public async Task RotateAsync_NewCurrentKey_ReencryptsTheContextsRows()
    {
        // Arrange
        Guid id = await AddNoteAsync("Groceries");

        // Act
        int reencrypted = await new EncryptionKeyRotator<TestEncryptedContext>(Factory(_secondKey),
                NullLogger<EncryptionKeyRotator<TestEncryptedContext>>.Instance)
            .RotateAsync(CancellationToken);

        // Assert
        await using TestEncryptedContext context = Factory(_secondKey).CreateDbContext();
        TestNote note = await context.Notes.SingleAsync(x => x.Id == id, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(reencrypted).IsEqualTo(1);
            await Assert.That(await ReadRawTextAsync(id)).StartsWith("2:");
            await Assert.That(note.Text).IsEqualTo("Groceries");
        }
    }

    private TestEncryptedContextFactory Factory(IColumnEncryptionService encryptionService) =>
        new(_connection, encryptionService);

    private async Task<Guid> AddNoteAsync(string text)
    {
        await using TestEncryptedContext context = Factory(_firstKey).CreateDbContext();
        TestNote note = new() { Text = text };
        context.Notes.Add(note);
        await context.SaveChangesAsync(CancellationToken);
        return note.Id;
    }

    private async Task<string> ReadRawTextAsync(Guid id)
    {
        await using TestEncryptedContext context = Factory(_firstKey).CreateDbContext();
        return await context.Database
            .SqlQuery<string>($"SELECT \"Text\" AS \"Value\" FROM \"Notes\" WHERE \"Id\" = {id}")
            .SingleAsync(CancellationToken);
    }
}
