using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Core;

public sealed class DuplicateScanner : IDuplicateScanner
{
    private readonly IFileSystemAbstraction fileSystem;
    private readonly IFileContentHasher hasher;
    private readonly IDuplicateIndex index;
    private readonly IDuplicateVerifier verifier;

    public DuplicateScanner(
        IFileSystemAbstraction fileSystem,
        IFileContentHasher hasher,
        IDuplicateIndex index,
        IDuplicateVerifier verifier)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        this.index = index ?? throw new ArgumentNullException(nameof(index));
        this.verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));

        index.Initialize();
    }

    public async Task<DuplicateDetectionResult> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(rootPath);

        var result = new DuplicateDetectionResult();

        var enumerableFiles = fileSystem.EnumerateFilesAsync(rootPath, "*", cancellationToken);

        await foreach (string path in enumerableFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath = Path.GetFullPath(path);

            if (!fileSystem.FileExists(fullPath))
            {
                continue;
            }

            var existing = await index.GetByPathAsync(fullPath, cancellationToken);

            // If the file already exists in the index, we skip further processing.
            if (existing is not null)
            {
                result.SkippedFilesCount++;
                continue;
            }

            long sizeBytes = fileSystem.GetFileSize(fullPath);
            var record = new FileRecordEntity
            {
                Path = fullPath,
                SizeBytes = sizeBytes,
                ModificationTimeStamp = fileSystem.GetLastWriteTimeUtcNanoseconds(fullPath),
                State = ScanState.NotScanned,
                SampleHash = null,
                FullHash = null,
                DuplicateOfFileId = null
            };

            result.NewOrUpdatedFilesCount++;

            IReadOnlyCollection<FileRecordEntity> candidates;
            // Small files are fully hashed
            if (sizeBytes <= DuplicateDetectionOptions.SmallFileFullHashThresholdBytes)
            {
                byte[] fullHash = await hasher.ComputeFullHashAsync(fullPath, cancellationToken);
                record.FullHash = fullHash;
                record.State = ScanState.FullHashCalculated;

                candidates = await index.GetBySizeAndFullHashAsync(sizeBytes, record.FullHash, cancellationToken);
            }
            // Large files are sample hashed
            else
            {
                byte[] sampleHash = await hasher.ComputeSampleHashAsync(fullPath, cancellationToken);
                record.SampleHash = sampleHash;
                record.State = ScanState.SampleHashCalculated;

                candidates = await index.GetBySizeAndSampleHashAsync(sizeBytes, record.SampleHash, cancellationToken);
            }

            bool confirmed = await ConfirmDuplicatesAsync(record, candidates, cancellationToken);

            if (confirmed)
            {
                result.ConfirmedDuplicatesCount++;

                await index.AddDuplicate(record, cancellationToken);
            }
            else
            {
                await index.AddCanonical(record, cancellationToken);
            }
        }

        return result;
    }

    private async Task<bool> ConfirmDuplicatesAsync(
        FileRecordEntity current,
        IReadOnlyCollection<FileRecordEntity> candidates,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string candidateFullPath = Path.GetFullPath(candidate.Path);
            string currentFullPath = Path.GetFullPath(current.Path);

            if (candidate.Id == current.Id 
                || string.Equals(candidateFullPath, currentFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Fatal error: duplicate verification requires two different physical files. Files: " 
                                                    + candidate.Path + " and " + current.Path);
            }

            if (candidate.SizeBytes != current.SizeBytes)
            {
                throw new InvalidOperationException("Fatal error: duplicate verification requires files of the same size. Files: " 
                                                    + candidate.Path + " and " + current.Path);
            }

            if (candidate.FullHash is null)
            {
                candidate.FullHash = await hasher.ComputeFullHashAsync(candidateFullPath, cancellationToken);
                candidate.State = ScanState.FullHashCalculated;

                await index.UpdateOrInsertAsync(candidate, cancellationToken);
            }

            if (current.FullHash == null)
            {
                current.FullHash = await hasher.ComputeFullHashAsync(currentFullPath, cancellationToken);
                current.State = ScanState.FullHashCalculated;
            }

            // If both files have full hashes, we can compare them directly.
            if (candidate.FullHash is not null && current.FullHash is not null)
            {
                if (!candidate.FullHash.SequenceEqual(current.FullHash))
                {
                    continue;
                }
            }

            // Byte-by-byte comparison is required to confirm duplicates, as hashes may collide.
            bool hasSameContent = await verifier.HasSameContentAsync(currentFullPath, candidateFullPath, cancellationToken);

            // Skip if the candidate does not have the same content.
            if (!hasSameContent)
            {
                continue;
            }

            current.DuplicateOfFileId = candidate.Id;
            current.State = ScanState.ConfirmedDuplicate;

            if (candidate.State != ScanState.ConfirmedDuplicate)
            {
                candidate.State = ScanState.Canonical;
            }

            // Persist only the canonical here. The confirmed duplicate is stored by the caller via
            // AddDuplicate so it lands in the separate duplicates table, not the canonical index.
            await index.UpdateOrInsertAsync(candidate, cancellationToken);

            return true;
        }

        return false;
    }
}
