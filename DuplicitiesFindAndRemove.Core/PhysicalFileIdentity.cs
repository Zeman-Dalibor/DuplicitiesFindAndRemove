using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DuplicitiesFindAndRemove.Core;

/// <summary>
/// Resolves and compares the physical identity of a file — the volume plus file id on
/// Windows and the device plus inode on Linux. Two different paths that resolve to the
/// same physical identity refer to the same on-disk object (hard link, symbolic link,
/// junction, or a case-only alias). Such aliases must never be treated as duplicates,
/// otherwise deleting the "duplicate" could remove the only real copy.
/// </summary>
public static class PhysicalFileIdentity
{
    /// <summary>
    /// Returns a stable identifier of the physical file behind <paramref name="path"/>, or
    /// <c>null</c> when the file cannot be opened or the file system does not expose one.
    /// Symbolic links and reparse points are followed to their target.
    /// </summary>
    public static string? Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            return ResolveWindows(path);
        }

        if (OperatingSystem.IsLinux())
        {
            return ResolveLinux(path);
        }

        return null;
    }

    /// <summary>
    /// Decides whether two paths point to the same physical file. When both identities are
    /// known they are compared directly; otherwise the decision falls back to an
    /// operating-system-aware path comparison so behavior degrades gracefully on file
    /// systems that do not expose an identity.
    /// </summary>
    public static bool AreSamePhysicalFile(string? identity1, string? identity2, string path1, string path2)
    {
        if (identity1 is not null && identity2 is not null)
        {
            return string.Equals(identity1, identity2, StringComparison.Ordinal);
        }

        return PathComparison.AreSamePath(path1, path2);
    }

    private static string? ResolveWindows(string path)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (!GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION info))
            {
                return null;
            }

            ulong fileIndex = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
            return $"winfileid:{info.VolumeSerialNumber:x8}:{fileIndex:x16}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? ResolveLinux(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "stat",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // -L dereferences symlinks so an alias resolves to its target's device:inode.
            startInfo.ArgumentList.Add("-L");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("%d:%i");
            startInfo.ArgumentList.Add(path);

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return process.ExitCode == 0 && output.Length > 0 ? $"unixinode:{output}" : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);
}
