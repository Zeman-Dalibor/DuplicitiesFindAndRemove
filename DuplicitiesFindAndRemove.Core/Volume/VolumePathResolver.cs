using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Resolves a file path into a stable volume identifier and a normalized relative path.
/// </summary>
public sealed class VolumePathResolver : IVolumePathResolver
{
    private readonly Dictionary<string, string?> volumeIdentityCache = new(StringComparer.OrdinalIgnoreCase);

    public VolumePathInfo Resolve(string filePath)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        string volumeRoot = GetVolumeRootPath(fullPath);
        string relativePath = RelativePathNormalizer.ToRelativePath(fullPath, volumeRoot);
        string volumeStableId = GetOrCreateVolumeStableId(volumeRoot);

        return new VolumePathInfo(volumeStableId, relativePath, volumeRoot);
    }

    private string GetOrCreateVolumeStableId(string volumeRootPath)
    {
        if (volumeIdentityCache.TryGetValue(volumeRootPath, out string? cached) && cached is not null)
        {
            return cached;
        }

        string? stableId = TryGetPartitionUuid(volumeRootPath)
            ?? TryGetVolumeSerial(volumeRootPath);

        if (stableId is null)
        {
            stableId = $"mountroot:{RelativePathNormalizer.NormalizeSeparators(volumeRootPath).ToLowerInvariant()}";
        }

        volumeIdentityCache[volumeRootPath] = stableId;
        return stableId;
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

    private static string? TryGetPartitionUuid(string volumeRootPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsVolumeIdentity.TryGetPartitionUuid(volumeRootPath);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxVolumeIdentity.TryGetPartitionUuid(volumeRootPath);
        }

        return null;
    }

    private static string? TryGetVolumeSerial(string volumeRootPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsVolumeIdentity.TryGetVolumeSerial(volumeRootPath);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxVolumeIdentity.TryGetFilesystemUuid(volumeRootPath);
        }

        return null;
    }

    private static class WindowsVolumeIdentity
    {
        public static string? TryGetPartitionUuid(string volumeRootPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            string? driveLetter = GetDriveLetter(volumeRootPath);
            if (driveLetter is null)
            {
                return null;
            }

            return QueryPartitionUuid(driveLetter);
        }

        [SupportedOSPlatform("windows")]
        public static string? TryGetVolumeSerial(string volumeRootPath)
        {
            if (!GetVolumeInformation(
                    volumeRootPath,
                    null,
                    0,
                    out uint serialNumber,
                    out _,
                    out _,
                    null,
                    0))
            {
                return null;
            }

            return $"volserial:{serialNumber:X8}";
        }

        private static string? GetDriveLetter(string volumeRootPath)
        {
            if (volumeRootPath.Length < 2 || volumeRootPath[1] != ':')
            {
                return null;
            }

            return volumeRootPath[0].ToString();
        }

        [SupportedOSPlatform("windows")]
        private static string? QueryPartitionUuid(string driveLetter)
        {
            try
            {
                string deviceId = $"{driveLetter}:";
                using var logicalDisk = new System.Management.ManagementObject($"Win32_LogicalDisk.DeviceID='{deviceId}'");
                logicalDisk.Get();

                foreach (System.Management.ManagementObject partition in logicalDisk.GetRelated("Win32_DiskPartition"))
                {
                    string? guid = partition["Guid"]?.ToString()?.Trim('{', '}');
                    if (!string.IsNullOrWhiteSpace(guid))
                    {
                        return $"partuuid:{guid.ToLowerInvariant()}";
                    }
                }
            }
            catch (System.Management.ManagementException)
            {
                return null;
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }

            return null;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder? volumeNameBuffer,
            int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder? fileSystemNameBuffer,
            int fileSystemNameSize);
    }

    private static class LinuxVolumeIdentity
    {
        public static string? TryGetPartitionUuid(string volumeRootPath)
        {
            string? devicePath = LinuxMountPointResolver.FindMountDevice(volumeRootPath);
            if (devicePath is null)
            {
                return null;
            }

            string? partUuid = RunLsblk(devicePath, "PARTUUID");
            if (!string.IsNullOrWhiteSpace(partUuid))
            {
                return $"partuuid:{partUuid.ToLowerInvariant()}";
            }

            return null;
        }

        public static string? TryGetFilesystemUuid(string volumeRootPath)
        {
            string? devicePath = LinuxMountPointResolver.FindMountDevice(volumeRootPath);
            if (devicePath is null)
            {
                return null;
            }

            string? fsUuid = RunLsblk(devicePath, "UUID");
            if (!string.IsNullOrWhiteSpace(fsUuid))
            {
                return $"fsuuid:{fsUuid.ToLowerInvariant()}";
            }

            return null;
        }

        private static string? RunLsblk(string devicePath, string column)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "lsblk",
                    Arguments = $"-rn -o {column} {devicePath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is null)
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                return process.ExitCode == 0 && output.Length > 0 ? output : null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                return null;
            }
        }
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

        public static string? FindMountDevice(string volumeRootPath)
        {
            string normalizedRoot = Path.GetFullPath(volumeRootPath);
            List<MountEntry> mounts = GetMounts();

            MountEntry? bestMatch = null;
            foreach (MountEntry mount in mounts)
            {
                if (!IsUnderMount(normalizedRoot, mount.MountPoint))
                {
                    continue;
                }

                if (bestMatch is null || mount.MountPoint.Length > bestMatch.MountPoint.Length)
                {
                    bestMatch = mount;
                }
            }

            return bestMatch?.DevicePath;
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
                string right = line[(separatorIndex + 3)..];
                string[] rightParts = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (rightParts.Length < 1)
                {
                    continue;
                }

                string devicePath = rightParts[0];
                mounts.Add(new MountEntry(mountPoint, devicePath));
            }

            return mounts;
        }

        private static string UnescapeMountPath(string mountPoint)
            => mountPoint.Replace(@"\040", " ", StringComparison.Ordinal)
                .Replace(@"\011", "\t", StringComparison.Ordinal)
                .Replace(@"\012", "\n", StringComparison.Ordinal)
                .Replace(@"\134", @"\", StringComparison.Ordinal);

        private sealed record MountEntry(string MountPoint, string DevicePath);
    }
}
