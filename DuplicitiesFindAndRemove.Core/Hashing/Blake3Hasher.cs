using Blake3;
using DuplicitiesFindAndRemove.Core.Interfaces;
using System.Buffers.Binary;

namespace DuplicitiesFindAndRemove.Core.Hashing;

public sealed class Blake3Hasher : IFileContentHasher
{
    private const int FullHashBufferSize = 1024 * 1024 * 4; // 4 MiB
    private const int SampleBlockSize = 64 * 1024;      // 64 KiB

    private static async Task<byte[]> ComputeFullHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var hasher = Hasher.New();
        byte[] buffer = new byte[FullHashBufferSize];

        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            hasher.Update(buffer.AsSpan(0, read));
        }

        return hasher.Finalize().AsSpan().ToArray();
    }

    public async Task<byte[]> ComputeFullHashAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(path);

        var options = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = FullHashBufferSize,
            Options = FileOptions.SequentialScan
        };

        await using var stream = new FileStream(path, options);
        return await ComputeFullHashAsync(stream, cancellationToken);
    }

    public async Task<byte[]> ComputeSampleHashAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(path);

        var options = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = SampleBlockSize,
            Options = FileOptions.SequentialScan
        };

        await using var stream = new FileStream(path, options);
        return await ComputeSampleHashAsync(stream, stream.Length, cancellationToken);
    }

    private static async Task<byte[]> ComputeSampleHashAsync(Stream stream, long sizeBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        }

        using var hasher = Hasher.New();

        byte[] sizeBuffer = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(sizeBuffer, sizeBytes);
        hasher.Update(sizeBuffer);

        if (sizeBytes <= (SampleBlockSize * 2L))
        {
            stream.Position = 0;
            byte[] all = await ReadExactlyAsync(stream, (int)sizeBytes, cancellationToken);
            hasher.Update(all);
            return hasher.Finalize().AsSpan().ToArray();
        }

        byte[] head = await ReadBlockAtAsync(stream, 0, SampleBlockSize, cancellationToken);
        byte[] tail = await ReadBlockAtAsync(stream, sizeBytes - SampleBlockSize, SampleBlockSize, cancellationToken);

        hasher.Update(head);
        hasher.Update(tail);

        return hasher.Finalize().AsSpan().ToArray();
    }

    private static async Task<byte[]> ReadBlockAtAsync(Stream stream, long offset, int count,
        CancellationToken cancellationToken)
    {
        stream.Position = offset;
        return await ReadExactlyAsync(stream, count, cancellationToken);
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int totalRead = 0;

        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), cancellationToken);
            if (read <= 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading file content.");
            }

            totalRead += read;
        }

        return buffer;
    }
}