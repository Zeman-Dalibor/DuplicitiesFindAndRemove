using System.Runtime.CompilerServices;
using System.Text;
using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IFileSystemAbstraction"/> used to drive the scanner without touching the disk.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystemAbstraction
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> identities = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryFileSystem AddFile(string path, string content)
        => AddFile(path, Encoding.UTF8.GetBytes(content));

    public InMemoryFileSystem AddFile(string path, byte[] content)
    {
        files[Path.GetFullPath(path)] = content;
        return this;
    }

    /// <summary>
    /// Adds a second path that references the same physical file as <paramref name="targetPath"/>,
    /// mimicking a hard link or symlink: same content and the same physical identity.
    /// </summary>
    public InMemoryFileSystem AddHardLink(string linkPath, string targetPath)
    {
        string target = Path.GetFullPath(targetPath);
        string link = Path.GetFullPath(linkPath);

        files[link] = ReadAllBytes(target);

        string identity = identities.TryGetValue(target, out string? existing) ? existing : target;
        identities[target] = identity;
        identities[link] = identity;
        return this;
    }

    public byte[] ReadAllBytes(string path)
    {
        if (files.TryGetValue(Path.GetFullPath(path), out byte[]? content))
        {
            return content;
        }

        throw new FileNotFoundException("File not found in the in-memory file system.", path);
    }

    public bool FileExists(string path) => files.ContainsKey(Path.GetFullPath(path));

    public bool DirectoryExists(string path) => true;

    public long GetFileSize(string path) => ReadAllBytes(path).Length;

    public long? GetLastWriteTimeUtcNanoseconds(string path) => 0;

    public Stream OpenRead(string path) => new MemoryStream(ReadAllBytes(path), writable: false);

    public async IAsyncEnumerable<string> EnumerateFilesAsync(
        string rootPath,
        string searchPattern = "*",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (string path in files.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return path;
        }

        await Task.CompletedTask;
    }

    public Task EnsureDirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        files.Remove(Path.GetFullPath(path));
        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        byte[] content = ReadAllBytes(sourcePath);
        files.Remove(Path.GetFullPath(sourcePath));
        files[Path.GetFullPath(destinationPath)] = content;
        return Task.CompletedTask;
    }

    public bool IsSameFilePath(string path1, string path2)
        => string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase);

    public string? GetFileIdentity(string path)
    {
        string full = Path.GetFullPath(path);
        if (!files.ContainsKey(full))
        {
            return null;
        }

        // Default identity is the normalized path, so distinct files are distinct.
        // Hard links share the identity registered via AddHardLink.
        return identities.TryGetValue(full, out string? identity) ? identity : full;
    }
}
