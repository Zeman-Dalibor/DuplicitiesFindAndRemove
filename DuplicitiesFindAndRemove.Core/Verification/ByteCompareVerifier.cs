using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Interfaces;
using System.Buffers;

namespace DuplicitiesFindAndRemove.Core.Verification;

public sealed class ByteCompareVerifier : IDuplicateVerifier
{
    private const int BufferSize = 1024 * 1024 * 64; // 64 MiB

    public async Task<bool> HasSameContentAsync(string firstPath, string secondPath, CancellationToken cancellationToken = default)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(firstPath);
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(secondPath);

        if (firstPath.Equals(secondPath) || firstPath == secondPath)
        {
            throw new InvalidOperationException($"FATAL ERROR: Paths to files are the same. Files: {firstPath}, {secondPath}.");
        }

        string firstFullPath = Path.GetFullPath(firstPath);
        string secondFullPath = Path.GetFullPath(secondPath);

        if (string.Equals(firstFullPath, secondFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"FATAL ERROR: verification requires two different physical files. Files: {firstPath}, {secondPath}.");
        }

        var firstInfo = new FileInfo(firstFullPath);
        var secondInfo = new FileInfo(secondFullPath);

        if (firstInfo.FullName == secondInfo.FullName)
        {
            throw new InvalidOperationException($"FATAL ERROR: Paths to files are the same. Files: {firstFullPath}, {secondFullPath}.");
        }

        if (!firstInfo.Exists || !secondInfo.Exists)
        {
            return false;
        }

        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = BufferSize,
            Options = FileOptions.SequentialScan
        };

        await using var firstStream = new FileStream(firstFullPath, options);
        await using var secondStream = new FileStream(secondFullPath, options);

        byte[] firstBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        byte[] secondBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            while (true)
            {
                int firstRead = await firstStream.ReadAsync(firstBuffer.AsMemory(0, BufferSize), cancellationToken);
                int secondRead = await secondStream.ReadAsync(secondBuffer.AsMemory(0, BufferSize), cancellationToken);

                if (firstRead != secondRead)
                {
                    return false;
                }

                if (firstRead == 0)
                {
                    return true;
                }

                if (!firstBuffer.AsSpan(0, firstRead)
                        .SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
                {
                    return false;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(firstBuffer, clearArray: true);
            ArrayPool<byte>.Shared.Return(secondBuffer, clearArray: true);
        }
    }

    public async Task<bool> HasSameContentAsync(FileRecordEntity fileA, FileRecordEntity fileB, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileA);
        ArgumentNullException.ThrowIfNull(fileB);

        return await HasSameContentAsync(fileA.Path, fileB.Path, cancellationToken);
    }
}
