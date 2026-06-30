using DuplicitiesFindAndRemove.Core.Interfaces;
using System.Runtime.CompilerServices;

namespace DuplicitiesFindAndRemove.Core;

/// <summary>
/// Production <see cref="IFileSystemAbstraction"/> backed by the real disk.
/// Directory enumeration is recursive and skips folders that cannot be accessed
/// so a single protected directory does not abort the whole scan.
/// </summary>
public sealed class FileSystemAbstraction : IFileSystemAbstraction
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public long? GetLastWriteTimeUtcNanoseconds(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        // DateTime ticks are 100-nanosecond units.
        return File.GetLastWriteTimeUtc(path).Ticks * 100L;
    }

    public Stream OpenRead(string path)
        => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    public async IAsyncEnumerable<string> EnumerateFilesAsync(
        string rootPath,
        string searchPattern = "*",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(rootPath);

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0 // do not skip hidden or system files
        };

        foreach (string file in Directory.EnumerateFiles(Path.GetFullPath(rootPath), searchPattern, enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }

        await Task.CompletedTask;
    }

    public Task EnsureDirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        File.Delete(path);
        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        File.Move(sourcePath, destinationPath);
        return Task.CompletedTask;
    }

    public bool IsSameFilePath(string path1, string path2)
        => string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase);
}
