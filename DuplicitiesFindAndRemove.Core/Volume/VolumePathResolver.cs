using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Resolves a file path into its portable <see cref="FileLocation"/>: the GUID of the disk it lives
/// on (taken from the disk's placeholder identity file) and the path relative to the disk root.
/// </summary>
public sealed class VolumePathResolver : IVolumePathResolver
{
    private readonly IDiskIdentityProvider diskIdentityProvider;

    public VolumePathResolver(IDiskIdentityProvider? diskIdentityProvider = null)
    {
        this.diskIdentityProvider = diskIdentityProvider ?? new DiskIdentityProvider();
    }

    public FileLocation Resolve(string filePath)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        string volumeRoot = GetVolumeRootPath(fullPath);
        string relativePath = RelativePathNormalizer.ToRelativePath(fullPath, volumeRoot);

        // The disk GUID comes solely from the placeholder identity file in the disk root. If that
        // file cannot be created or read, this throws; there is intentionally no fallback identity.
        DiskIdentity identity = diskIdentityProvider.GetOrCreate(volumeRoot);

        return new FileLocation(identity.Id, relativePath);
    }

    private static string GetVolumeRootPath(string fullPath)
    {
        if (OperatingSystem.IsLinux())
        {
            return LinuxMountPointResolver.FindLongestMountPoint(fullPath);
        }

        if (IsUncPath(fullPath))
        {
            return GetUncShareRoot(fullPath);
        }

        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException($"Unable to determine volume root for path '{fullPath}'.");
        }

        return root;
    }

    private static bool IsUncPath(string path)
        => path.StartsWith(@"\\", StringComparison.Ordinal);

    private static string GetUncShareRoot(string fullPath)
    {
        string trimmed = fullPath.TrimEnd('\\', '/');
        ReadOnlySpan<char> span = trimmed.AsSpan(2);
        int slashIndex = span.IndexOf('\\');
        if (slashIndex < 0)
        {
            return trimmed + Path.DirectorySeparatorChar;
        }

        int secondSlashIndex = span[(slashIndex + 1)..].IndexOf('\\');
        if (secondSlashIndex < 0)
        {
            return trimmed + Path.DirectorySeparatorChar;
        }

        int shareEnd = 2 + slashIndex + 1 + secondSlashIndex;
        return trimmed[..shareEnd] + Path.DirectorySeparatorChar;
    }

    private static class LinuxMountPointResolver
    {
        private static readonly object Sync = new();
        private static List<MountEntry>? cachedMounts;

        public static string FindLongestMountPoint(string fullPath)
        {
            string normalizedPath = Path.GetFullPath(fullPath);
            List<MountEntry> mounts = GetMounts();

            MountEntry? bestMatch = null;
            foreach (MountEntry mount in mounts)
            {
                if (!IsUnderMount(normalizedPath, mount.MountPoint))
                {
                    continue;
                }

                if (bestMatch is null || mount.MountPoint.Length > bestMatch.MountPoint.Length)
                {
                    bestMatch = mount;
                }
            }

            if (bestMatch is null)
            {
                throw new InvalidOperationException($"Unable to determine mount point for path '{fullPath}'.");
            }

            return bestMatch.MountPoint;
        }

        private static bool IsUnderMount(string fullPath, string mountPoint)
        {
            string normalizedMount = Path.GetFullPath(mountPoint);
            if (!normalizedMount.EndsWith(Path.DirectorySeparatorChar))
            {
                normalizedMount += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(normalizedMount, StringComparison.Ordinal);
        }

        private static List<MountEntry> GetMounts()
        {
            lock (Sync)
            {
                if (cachedMounts is not null)
                {
                    return cachedMounts;
                }

                cachedMounts = ParseMountInfo("/proc/self/mountinfo");
                return cachedMounts;
            }
        }

        private static List<MountEntry> ParseMountInfo(string mountInfoPath)
        {
            var mounts = new List<MountEntry>();

            foreach (string line in File.ReadLines(mountInfoPath))
            {
                int separatorIndex = line.IndexOf(" - ", StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    continue;
                }

                string left = line[..separatorIndex];
                string[] leftParts = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (leftParts.Length < 5)
                {
                    continue;
                }

                string mountPoint = UnescapeMountPath(leftParts[4]);
                mounts.Add(new MountEntry(mountPoint));
            }

            return mounts;
        }

        private static string UnescapeMountPath(string mountPoint)
            => mountPoint.Replace(@"\040", " ", StringComparison.Ordinal)
                .Replace(@"\011", "\t", StringComparison.Ordinal)
                .Replace(@"\012", "\n", StringComparison.Ordinal)
                .Replace(@"\134", @"\", StringComparison.Ordinal);

        private sealed record MountEntry(string MountPoint);
    }
}
