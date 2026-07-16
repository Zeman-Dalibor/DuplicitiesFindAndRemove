using DuplicitiesFindAndRemove.Core.FileSystemHelpers;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public sealed class FileSystemAbstractionTests : IDisposable
{
    private readonly string root;
    private readonly FileSystemAbstraction fileSystem = new();

    public FileSystemAbstractionTests()
    {
        root = Path.Combine(Path.GetTempPath(), "fsabstraction-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<List<string>> EnumerateAsync()
    {
        var results = new List<string>();
        await foreach (string path in fileSystem.EnumerateFilesAsync(root))
        {
            results.Add(path);
        }

        return results;
    }

    [Fact]
    public async Task EnumerateFilesAsync_ReturnsRegularFiles_IncludingHiddenAndNested()
    {
        string plain = Path.Combine(root, "plain.txt");
        File.WriteAllText(plain, "a");

        string nestedDir = Path.Combine(root, "nested");
        Directory.CreateDirectory(nestedDir);
        string nested = Path.Combine(nestedDir, "nested.txt");
        File.WriteAllText(nested, "b");

        string hidden = Path.Combine(root, "hidden.txt");
        File.WriteAllText(hidden, "c");
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

        List<string> results = await EnumerateAsync();

        Assert.Contains(plain, results);
        Assert.Contains(nested, results);
        Assert.Contains(hidden, results);
    }

    [Fact]
    public async Task EnumerateFilesAsync_SkipsSymbolicLinkFiles()
    {
        string target = Path.Combine(root, "target.txt");
        File.WriteAllText(target, "content");

        string link = Path.Combine(root, "link.txt");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Creating symlinks needs elevated rights / developer mode on Windows.
            // Skip the assertion when the environment does not allow it.
            return;
        }

        List<string> results = await EnumerateAsync();

        Assert.Contains(target, results);
        Assert.DoesNotContain(link, results);
    }

    [Fact]
    public async Task EnumerateFilesAsync_DoesNotFollowSymbolicLinkDirectories()
    {
        string realDir = Path.Combine(root, "real");
        Directory.CreateDirectory(realDir);
        File.WriteAllText(Path.Combine(realDir, "inside.txt"), "x");

        string linkedDir = Path.Combine(root, "linked");
        try
        {
            Directory.CreateSymbolicLink(linkedDir, realDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        List<string> results = await EnumerateAsync();

        // The real file is found exactly once, never a second time via the linked directory.
        Assert.Single(results, path => Path.GetFileName(path) == "inside.txt");
    }
}
