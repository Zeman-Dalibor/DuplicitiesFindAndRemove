using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Tests.Fakes;

/// <summary>
/// Verifier that compares the in-memory content of two files byte for byte.
/// </summary>
internal sealed class FakeDuplicateVerifier : IDuplicateVerifier
{
    private readonly InMemoryFileSystem fileSystem;

    public FakeDuplicateVerifier(InMemoryFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public Task<bool> HasSameContentAsync(string pathA, string pathB, CancellationToken cancellationToken = default)
    {
        byte[] contentA = fileSystem.ReadAllBytes(pathA);
        byte[] contentB = fileSystem.ReadAllBytes(pathB);
        return Task.FromResult(contentA.SequenceEqual(contentB));
    }

    public Task<bool> HasSameContentAsync(FileRecordEntity fileA, FileRecordEntity fileB, CancellationToken cancellationToken = default)
        => HasSameContentAsync(fileA.Path, fileB.Path, cancellationToken);
}
