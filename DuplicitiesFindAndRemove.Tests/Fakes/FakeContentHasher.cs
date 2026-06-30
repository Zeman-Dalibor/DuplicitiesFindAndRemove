using System.Security.Cryptography;
using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Tests.Fakes;

/// <summary>
/// Deterministic hasher backed by <see cref="InMemoryFileSystem"/>. The full hash depends on the
/// whole content, while the sample hash depends only on the file size, so two same-sized files
/// collide and require byte-by-byte verification (just like the real sampling strategy).
/// </summary>
internal sealed class FakeContentHasher : IFileContentHasher
{
    private readonly InMemoryFileSystem fileSystem;

    public FakeContentHasher(InMemoryFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public Task<byte[]> ComputeFullHashAsync(string path, CancellationToken cancellationToken = default)
    {
        byte[] content = fileSystem.ReadAllBytes(path);
        return Task.FromResult(SHA256.HashData(content));
    }

    public Task<byte[]> ComputeSampleHashAsync(string path, CancellationToken cancellationToken = default)
    {
        byte[] content = fileSystem.ReadAllBytes(path);
        byte[] sizeBytes = BitConverter.GetBytes((long)content.Length);
        return Task.FromResult(SHA256.HashData(sizeBytes));
    }
}
