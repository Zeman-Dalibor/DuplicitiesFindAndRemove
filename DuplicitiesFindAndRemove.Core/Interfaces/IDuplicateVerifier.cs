using DuplicitiesFindAndRemove.Core.Database;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IDuplicateVerifier
{
    Task<bool> HasSameContentAsync(
        string pathA,
        string pathB,
        CancellationToken cancellationToken = default);
}
