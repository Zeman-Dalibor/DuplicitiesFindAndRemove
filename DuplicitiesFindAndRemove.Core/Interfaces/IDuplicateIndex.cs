using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IDuplicateIndex
{
    void Initialize();

    Task<FileRecordEntity?> GetByLocationAsync(FileLocation location, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FileRecordEntity>> GetBySize(long sizeBytes, CancellationToken cancellationToken);

    Task<IReadOnlyList<FileRecordEntity>> GetBySizeAndSampleHashAsync(
        long sizeBytes,
        byte[] sampleHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FileRecordEntity>> GetBySizeAndFullHashAsync(long sizeBytes, byte[] fullHash, CancellationToken cancellationToken);

    Task UpdateOrInsertAsync(FileRecordEntity record, CancellationToken cancellationToken = default);

    void SetFileAsModified(FileRecordEntity record);

    Task AddDuplicate(FileRecordEntity duplicate, CancellationToken cancellationToken);

    Task AddCanonical(FileRecordEntity record, CancellationToken cancellationToken);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
