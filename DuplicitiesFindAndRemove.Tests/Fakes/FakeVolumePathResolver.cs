using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Tests.Fakes;

/// <summary>
/// Deterministic volume resolver for scanner tests that do not touch the real file system.
/// </summary>
internal sealed class FakeVolumePathResolver : IVolumePathResolver
{
    public VolumePathInfo Resolve(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        string volumeRoot = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"Unable to determine volume root for '{filePath}'.");

        string relativePath = RelativePathNormalizer.ToRelativePath(fullPath, volumeRoot);
        string volumeStableId = $"testvol:{RelativePathNormalizer.NormalizeSeparators(volumeRoot).ToLowerInvariant()}";

        return new VolumePathInfo(volumeStableId, relativePath, volumeRoot);
    }
}
