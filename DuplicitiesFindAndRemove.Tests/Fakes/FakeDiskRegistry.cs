using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Tests.Fakes;

/// <summary>
/// Deterministic disk registry for scanner tests. It mirrors <see cref="FakeVolumePathResolver"/>:
/// each mounted drive root maps to the same deterministic disk GUID, so a portable
/// <see cref="FileLocation"/> reconstructs back to its original absolute path.
/// </summary>
internal sealed class FakeDiskRegistry : IDiskRegistry
{
    private readonly Dictionary<Guid, string> rootsById = new();

    public FakeDiskRegistry()
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            string root = drive.RootDirectory.FullName;
            rootsById[TestVolumes.DiskIdForRoot(root)] = root;
        }
    }

    public string? TryGetDiskRoot(Guid diskId)
        => rootsById.TryGetValue(diskId, out string? root) ? root : null;

    public string? TryGetAbsolutePath(FileLocation location)
    {
        string? root = TryGetDiskRoot(location.DiskId);
        if (root is null)
        {
            return null;
        }

        string relative = location.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(root, relative));
    }
}
