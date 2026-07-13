using DuplicitiesFindAndRemove.Core.Volume;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests.Volume;

public class VolumePathResolverTests
{
    [Fact]
    public void Resolve_PopulatesRelativePath_ForExistingFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "volume-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string filePath = Path.Combine(directory, "nested", "file.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "content");

            var resolver = new VolumePathResolver();
            VolumePathInfo info = resolver.Resolve(filePath);

            Assert.False(string.IsNullOrWhiteSpace(info.VolumeStableId));
            Assert.EndsWith("nested/file.txt", info.RelativePath, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(info.VolumeRootPath));
            Assert.StartsWith(info.VolumeRootPath, filePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
