using DuplicitiesFindAndRemove.Core.Database;

namespace DuplicitiesFindAndRemove.Core;

public interface IDuplicateVerifier
{
    Task<bool> HasSameContentAsync(
        string pathA,
        string pathB,
        CancellationToken cancellationToken = default);

    Task<bool> HasSameContentAsync(
        FileRecordEntity fileA,
        FileRecordEntity fileB,
        CancellationToken cancellationToken = default);
}
