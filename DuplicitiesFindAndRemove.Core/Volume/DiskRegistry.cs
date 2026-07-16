using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Builds, at startup, a map of disk GUID to the disk's current mount root by inspecting the
/// mounted disks and reading (or creating) their placeholder identity files. The map lets stored
/// <see cref="FileLocation"/> values be reconstructed into absolute paths on this machine.
/// </summary>
public sealed class DiskRegistry : IDiskRegistry
{
    private readonly Dictionary<Guid, string> rootsById = new();

    public DiskRegistry(IDiskIdentityProvider diskIdentityProvider)
    {
        if (diskIdentityProvider is null)
        {
            throw new ArgumentNullException(nameof(diskIdentityProvider));
        }

        Build(diskIdentityProvider);
    }

    public string? TryGetDiskRoot(Guid diskId)
        => rootsById.TryGetValue(diskId, out string? root) ? root : null;

    public string? TryGetAbsolutePath(FileLocation location)
    {
        if (location is null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        string? root = TryGetDiskRoot(location.DiskId);
        if (root is null)
        {
            return null;
        }

        string relative = location.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(root, relative));
    }

    private void Build(IDiskIdentityProvider diskIdentityProvider)
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            string root = drive.RootDirectory.FullName;

            try
            {
                DiskIdentity identity = diskIdentityProvider.GetOrCreate(root);
                rootsById[identity.Id] = root;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
            {
                // A disk whose root cannot be written (e.g. a read-only volume) simply is not part
                // of the map. Files on such disks cannot be reconstructed until it becomes writable.
            }
        }
    }
}
