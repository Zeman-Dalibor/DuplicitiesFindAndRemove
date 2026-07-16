using System.Security.Cryptography;
using System.Text;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Tests.Fakes;

/// <summary>
/// Shared helpers for the in-memory test doubles so that <see cref="FakeVolumePathResolver"/> and
/// <see cref="FakeDiskRegistry"/> agree on how a disk root maps to a deterministic disk GUID.
/// </summary>
internal static class TestVolumes
{
    public static string NormalizeRoot(string volumeRoot)
        => RelativePathNormalizer.NormalizeSeparators(volumeRoot).ToLowerInvariant();

    public static Guid DiskIdForRoot(string volumeRoot)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(NormalizeRoot(volumeRoot)));
        return new Guid(hash);
    }
}
