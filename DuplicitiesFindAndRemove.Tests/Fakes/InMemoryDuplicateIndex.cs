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
