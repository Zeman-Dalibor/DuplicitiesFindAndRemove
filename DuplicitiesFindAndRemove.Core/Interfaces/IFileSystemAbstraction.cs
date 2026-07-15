using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IFileSystemAbstraction
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    long GetFileSize(string path);

    long? GetLastWriteTimeUtcNanoseconds(string path);

    Stream OpenRead(string path);

    IAsyncEnumerable<string> EnumerateFilesAsync(
        string rootPath,
        string searchPattern = "*",
        CancellationToken cancellationToken = default);

    Task EnsureDirectoryExistsAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task MoveFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    bool IsSameFilePath(string path1, string path2);

    /// <summary>
    /// Returns a stable identifier of the physical file behind <paramref name="path"/>
    /// (volume + file id on Windows, device + inode on Linux), or <c>null</c> when the
    /// file system does not expose one. Two paths sharing the same non-null identity
    /// refer to the same physical file (hard link, symlink, or alias).
    /// </summary>
    string? GetFileIdentity(string path);
}
