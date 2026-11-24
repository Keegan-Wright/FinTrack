namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

/// <summary>
/// A forward-only stream that refuses synchronous writes and flushes, as an HTTP response body does, over a
/// <see cref="MemoryStream"/> to read back.
/// </summary>
internal sealed class AsyncWritesOnlyStream : Stream
{
    /// <summary>
    /// What was written.
    /// </summary>
    public MemoryStream Written { get; } = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new InvalidOperationException("Synchronous flushes aren't allowed");

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException("Synchronous writes aren't allowed");

    public override void Write(ReadOnlySpan<byte> buffer) =>
        throw new InvalidOperationException("Synchronous writes aren't allowed");

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Written.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        Written.WriteAsync(buffer, cancellationToken);
}
