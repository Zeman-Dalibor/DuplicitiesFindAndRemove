using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Tests.Fakes;

/// <summary>
/// Simple list-backed <see cref="IDuplicateIndex"/> that mimics the database during scanner tests.
/// </summary>
internal sealed class InMemoryDuplicateIndex : IDuplicateIndex
{
    private readonly List<FileRecordEntity> records = new();
    private long nextId = 1;

    public IReadOnlyList<FileRecordEntity> Records => records;

    public Task<FileRecordEntity?> GetByLocationAsync(FileLocation location, CancellationToken cancellationToken = default)
        => Task.FromResult(records.FirstOrDefault(record => record.Location == location));

    public async Task<IReadOnlyCollection<FileRecordEntity>> GetBySize(long sizeBytes, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FileRecordEntity> matches = records
            .Where(record => record.SizeBytes == sizeBytes)
            .ToList();

        return await Task.FromResult(matches);
    }

    public Task<IReadOnlyList<FileRecordEntity>> GetBySizeAndSampleHashAsync(
        long sizeBytes,
        byte[] sampleHash,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FileRecordEntity> matches = records
            .Where(record => record.SizeBytes == sizeBytes
                && record.SampleHash is not null
                && record.SampleHash.SequenceEqual(sampleHash))
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<IReadOnlyCollection<FileRecordEntity>> GetBySizeAndFullHashAsync(long sizeBytes, byte[] fullHash, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FileRecordEntity> matches = records
            .Where(record => record.SizeBytes == sizeBytes
                && record.FullHash is not null
                && record.FullHash.SequenceEqual(fullHash))
            .ToList();

        return Task.FromResult(matches);
    }

    public Task UpdateOrInsertAsync(FileRecordEntity record, CancellationToken cancellationToken = default)
    {
        Insert(record);
        return Task.CompletedTask;
    }

    public Task AddDuplicate(FileRecordEntity duplicate, CancellationToken cancellationToken)
    {
        Insert(duplicate);
        return Task.CompletedTask;
    }

    public Task AddCanonical(FileRecordEntity record, CancellationToken cancellationToken)
    {
        Insert(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FileRecordEntity>> GetCanonicalRecordsUnderLocationAsync(
        FileLocation directoryLocation,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FileRecordEntity> matches = records
            .Where(record => record.DuplicateOfFileId is null
                && record.DiskId == directoryLocation.DiskId
                && IndexPathFilter.IsUnderDirectory(record.RelativePath, directoryLocation.RelativePath))
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<DuplicateRecordEntity>> GetDuplicateRecordsUnderLocationAsync(
        FileLocation directoryLocation,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DuplicateRecordEntity> matches = records
            .Where(record => record.DuplicateOfFileId is not null
                && record.DiskId == directoryLocation.DiskId
                && IndexPathFilter.IsUnderDirectory(record.RelativePath, directoryLocation.RelativePath))
            .Select(record => new DuplicateRecordEntity
            {
                Id = record.Id,
                DiskId = record.DiskId,
                RelativePath = record.RelativePath,
                SizeBytes = record.SizeBytes,
                SampleHash = record.SampleHash,
                FullHash = record.FullHash,
                DuplicateOfFileId = record.DuplicateOfFileId!.Value,
                State = record.State,
                ModificationTimeStamp = record.ModificationTimeStamp
            })
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<int> DeleteCanonicalWithDuplicatesAsync(long canonicalId, CancellationToken cancellationToken = default)
    {
        int cascadeDeleted = records.RemoveAll(record =>
            record.DuplicateOfFileId == canonicalId);

        records.RemoveAll(record => record.Id == canonicalId && record.DuplicateOfFileId is null);
        return Task.FromResult(cascadeDeleted);
    }

    public Task DeleteDuplicateAsync(long duplicateId, CancellationToken cancellationToken = default)
    {
        records.RemoveAll(record => record.Id == duplicateId && record.DuplicateOfFileId is not null);
        return Task.CompletedTask;
    }

    public Task UpdateDuplicateAsync(DuplicateRecordEntity duplicate, CancellationToken cancellationToken = default)
    {
        FileRecordEntity? existing = records.FirstOrDefault(record => record.Id == duplicate.Id);
        if (existing is null)
        {
            return Task.CompletedTask;
        }

        existing.SizeBytes = duplicate.SizeBytes;
        existing.SampleHash = duplicate.SampleHash;
        existing.FullHash = duplicate.FullHash;
        existing.State = duplicate.State;
        existing.ModificationTimeStamp = duplicate.ModificationTimeStamp;
        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void SetFileAsModified(FileRecordEntity record)
    {
    }

    private void Insert(FileRecordEntity record)
    {
        if (records.Contains(record))
        {
            return;
        }

        if (record.Id == 0)
        {
            record.Id = nextId++;
        }

        records.Add(record);
    }

    public void Initialize()
    {
        records.Clear();
        nextId = 1;
    }
}
