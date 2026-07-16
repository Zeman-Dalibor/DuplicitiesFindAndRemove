using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.FileSystemHelpers;
using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core;

public sealed class IndexChecker : IIndexChecker
{
    private const int ProgressReportInterval = 1000;

    private readonly IFileSystemAbstraction fileSystem;
    private readonly IFileContentHasher hasher;
    private readonly IDuplicateIndex index;
    private readonly IVolumePathResolver volumePathResolver;
    private readonly IDiskRegistry diskRegistry;

    public IndexChecker(
        IFileSystemAbstraction fileSystem,
        IFileContentHasher hasher,
        IDuplicateIndex index,
        IVolumePathResolver volumePathResolver,
        IDiskRegistry diskRegistry)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        this.index = index ?? throw new ArgumentNullException(nameof(index));
        this.volumePathResolver = volumePathResolver ?? throw new ArgumentNullException(nameof(volumePathResolver));
        this.diskRegistry = diskRegistry ?? throw new ArgumentNullException(nameof(diskRegistry));

        index.Initialize();
    }

    public async Task<IndexCheckResult> CheckAsync(string filterDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(filterDirectory);

        string fullFilterPath = Path.GetFullPath(filterDirectory);
        FileLocation filterLocation = volumePathResolver.Resolve(fullFilterPath);

        var result = new IndexCheckResult();
        int processedRecordsCount = 0;

        IReadOnlyList<FileRecordEntity> canonicalRecords =
            await index.GetCanonicalRecordsUnderLocationAsync(filterLocation, cancellationToken);

        foreach (FileRecordEntity canonical in canonicalRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await CheckCanonicalAsync(canonical, result, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Checking canonical record id={canonical.Id} at {canonical.Location} failed:");
                Console.WriteLine(ex);
                result.ErrorRecordsCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Checking canonical record id={canonical.Id} at {canonical.Location} failed:");
                Console.WriteLine(ex);
                result.ErrorRecordsCount++;
            }

            processedRecordsCount++;
            if (processedRecordsCount % ProgressReportInterval == 0)
            {
                await index.FlushAsync(cancellationToken);
                Console.WriteLine($"Checked {processedRecordsCount} records.");
            }
        }

        IReadOnlyList<DuplicateRecordEntity> duplicateRecords =
            await index.GetDuplicateRecordsUnderLocationAsync(filterLocation, cancellationToken);

        foreach (DuplicateRecordEntity duplicate in duplicateRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await CheckDuplicateAsync(duplicate, result, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Checking duplicate record id={duplicate.Id} at {duplicate.Location} failed:");
                Console.WriteLine(ex);
                result.ErrorRecordsCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Checking duplicate record id={duplicate.Id} at {duplicate.Location} failed:");
                Console.WriteLine(ex);
                result.ErrorRecordsCount++;
            }

            processedRecordsCount++;
            if (processedRecordsCount % ProgressReportInterval == 0)
            {
                await index.FlushAsync(cancellationToken);
                Console.WriteLine($"Checked {processedRecordsCount} records.");
            }
        }

        await index.FlushAsync(cancellationToken);
        return result;
    }

    private async Task CheckCanonicalAsync(
        FileRecordEntity canonical,
        IndexCheckResult result,
        CancellationToken cancellationToken)
    {
        string? absolutePath = diskRegistry.TryGetAbsolutePath(canonical.Location);
        if (absolutePath is null)
        {
            result.UnmountedRecordsCount++;
            return;
        }

        FileMetadata? metadata = fileSystem.GetFileMetadata(absolutePath);
        if (metadata is null)
        {
            int cascadeDeleted = await index.DeleteCanonicalWithDuplicatesAsync(canonical.Id, cancellationToken);
            result.DeletedCanonicalRecordsCount++;
            result.CascadeDeletedDuplicatesCount += cascadeDeleted;
            return;
        }

        if (!HasMetadataChanged(canonical, metadata.Value))
        {
            result.UnchangedRecordsCount++;
            return;
        }

        await RecalculateHashesAsync(canonical, absolutePath, metadata.Value, cancellationToken);
        await index.UpdateOrInsertAsync(canonical, cancellationToken);
        result.UpdatedRecordsCount++;
    }

    private async Task CheckDuplicateAsync(
        DuplicateRecordEntity duplicate,
        IndexCheckResult result,
        CancellationToken cancellationToken)
    {
        string? absolutePath = diskRegistry.TryGetAbsolutePath(duplicate.Location);
        if (absolutePath is null)
        {
            result.UnmountedRecordsCount++;
            return;
        }

        FileMetadata? metadata = fileSystem.GetFileMetadata(absolutePath);
        if (metadata is null)
        {
            await index.DeleteDuplicateAsync(duplicate.Id, cancellationToken);
            result.DeletedDuplicateRecordsCount++;
            return;
        }

        if (!HasMetadataChanged(duplicate, metadata.Value))
        {
            result.UnchangedRecordsCount++;
            return;
        }

        await RecalculateHashesAsync(duplicate, absolutePath, metadata.Value, cancellationToken);
        await index.UpdateDuplicateAsync(duplicate, cancellationToken);
        result.UpdatedRecordsCount++;
    }

    private static bool HasMetadataChanged(IFileRecord record, FileMetadata metadata)
        => record.SizeBytes != metadata.SizeBytes
           || record.ModificationTimeStamp != metadata.LastWriteTimeUtcNanoseconds;

    private async Task RecalculateHashesAsync(
        IFileRecord record,
        string absolutePath,
        FileMetadata metadata,
        CancellationToken cancellationToken)
    {
        record.SizeBytes = metadata.SizeBytes;
        record.ModificationTimeStamp = metadata.LastWriteTimeUtcNanoseconds;
        record.FullHash = null;

        if (metadata.SizeBytes <= DuplicateDetectionOptions.SmallFileFullHashThresholdBytes)
        {
            record.SampleHash = null;
            record.FullHash = await hasher.ComputeFullHashAsync(absolutePath, cancellationToken);
            record.State = ScanState.FullHashCalculated;
            return;
        }

        record.SampleHash = await hasher.ComputeSampleHashAsync(absolutePath, cancellationToken);
        record.State = ScanState.SampleHashCalculated;
    }
}
