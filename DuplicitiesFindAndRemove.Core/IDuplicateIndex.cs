using DuplicitiesFindAndRemove.Core.Database;

namespace DuplicitiesFindAndRemove.Core;

public interface IDuplicateIndex
{
    Task<FileRecordEntity?> GetByPathAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileRecordEntity>> GetBySizeAndSampleHashAsync(
        long sizeBytes,
        byte[] sampleHash,
        CancellationToken cancellationToken = default);

    Task UpdateOrInsertAsync(FileRecordEntity record, CancellationToken cancellationToken = default);

    void SetFileAsModified(FileRecordEntity record);
    
    Task<IReadOnlyCollection<FileRecordEntity>> GetBySizeAndFullHashAsync(long sizeBytes, byte[] fullHash, CancellationToken cancellationToken);

    Task AddDuplicate(FileRecordEntity duplicate, CancellationToken cancellationToken);

    Task AddCanonical(FileRecordEntity record, CancellationToken cancellationToken);
}
