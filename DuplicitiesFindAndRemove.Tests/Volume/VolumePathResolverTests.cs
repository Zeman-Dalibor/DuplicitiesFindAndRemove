using DuplicitiesFindAndRemove.Core.Volume;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests.Volume;

public class VolumePathResolverTests
{
    [Fact]
    public void Resolve_ProducesDiskIdAndRelativePath_ForExistingFile()
    {
        // The resolver writes the placeholder identity file to the drive root, which requires
        // write access there (elevation on the Windows system drive).
        if (!TestEnvironment.IsDriveRootWritable(Path.GetTempPath()))
        {
            return;
        }

        string directory = Path.Combine(Path.GetTempPath(), "volume-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string filePath = Path.Combine(directory, "nested", "file.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "content");

            var resolver = new VolumePathResolver();
            FileLocation location = resolver.Resolve(filePath);

            Assert.NotEqual(Guid.Empty, location.DiskId);
            Assert.EndsWith("nested/file.txt", location.RelativePath, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
