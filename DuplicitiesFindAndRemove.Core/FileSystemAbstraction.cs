using DuplicitiesFindAndRemove.Core.Interfaces;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;

namespace DuplicitiesFindAndRemove.Core;

/// <summary>
/// Production <see cref="IFileSystemAbstraction"/> backed by the real disk.
/// Directory enumeration is recursive and skips folders that cannot be accessed
/// so a single protected directory does not abort the whole scan. Genuine links
/// (symbolic links and junctions) are skipped to avoid cycles and double scanning,
/// while other reparse points (deduplicated, compressed or cloud-backed files) are
/// treated as normal data files.
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
            // Clear the default Hidden|System skip so those files are still scanned. Links are
            // handled explicitly below, because a reparse-point attribute alone does not identify
            // a link: deduplicated, compressed (WOF) and cloud-backed files carry it too.
            AttributesToSkip = 0
        };

        var enumerable = new FileSystemEnumerable<string>(
            Path.GetFullPath(rootPath),
            static (ref FileSystemEntry entry) => entry.ToFullPath(),
            enumerationOptions)
        {
            // Return real files only. Genuine links (symlinks/junctions) are skipped so their
            // target is not scanned twice; other reparse points are treated as normal files.
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                !entry.IsDirectory
                && FileSystemName.MatchesSimpleExpression(searchPattern, entry.FileName)
                && !IsLink(ref entry),

            // Never descend into linked directories, which would risk cycles and double scans.
            ShouldRecursePredicate = static (ref FileSystemEntry entry) => !IsLink(ref entry)
        };

        foreach (string file in enumerable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }

        await Task.CompletedTask;
    }

    // A reparse-point attribute alone does not mean the entry is a link — deduplicated,
    // WOF-compressed and cloud-backed files also carry it. FileSystemInfo.LinkTarget is
    // non-null only for genuine name-surrogate links (symbolic links and junctions), so it
    // distinguishes links that must not be followed from real data files.
    private static bool IsLink(ref FileSystemEntry entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) == 0)
        {
            return false;
        }

        string fullPath = entry.ToFullPath();
        try
        {
            FileSystemInfo info = entry.IsDirectory
                ? new DirectoryInfo(fullPath)
                : new FileInfo(fullPath);

            return info.LinkTarget is not null;
        }
        catch (IOException)
        {
            // Unresolvable reparse point: treat it as a link so it is not followed.
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
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
        => PathComparison.AreSamePath(path1, path2);

    public string? GetFileIdentity(string path) => PhysicalFileIdentity.Resolve(path);
}
