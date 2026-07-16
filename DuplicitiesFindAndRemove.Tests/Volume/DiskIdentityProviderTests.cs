using DuplicitiesFindAndRemove.Core.Volume;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests.Volume;

public class DiskIdentityProviderTests
{
    [Fact]
    public void GetOrCreate_CreatesPlaceholderFile_WhenMissing()
    {
        string root = CreateTempDirectory();
        try
        {
            var provider = new DiskIdentityProvider();

            DiskIdentity? identity = provider.GetOrCreate(root);

            Assert.NotNull(identity);
            Assert.NotEqual(Guid.Empty, identity!.Id);
            Assert.True(File.Exists(Path.Combine(root, DiskIdentityProvider.PlaceholderFileName)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetOrCreate_ReturnsExistingIdentity_WhenPlaceholderFileAlreadyExists()
    {
        string root = CreateTempDirectory();
        try
        {
            DiskIdentity? first = new DiskIdentityProvider().GetOrCreate(root);
            DiskIdentity? second = new DiskIdentityProvider().GetOrCreate(root);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Id, second!.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetOrCreate_ReturnsSameCachedIdentity_OnRepeatedCalls()
    {
        string root = CreateTempDirectory();
        try
        {
            var provider = new DiskIdentityProvider();

            DiskIdentity? first = provider.GetOrCreate(root);
            DiskIdentity? second = provider.GetOrCreate(root);

            Assert.Same(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetOrCreate_RewritesIdentity_WhenPlaceholderFileIsCorrupt()
    {
        string root = CreateTempDirectory();
        try
        {
            string filePath = Path.Combine(root, DiskIdentityProvider.PlaceholderFileName);
            File.WriteAllText(filePath, "not valid json");

            DiskIdentity? identity = new DiskIdentityProvider().GetOrCreate(root);

            Assert.NotNull(identity);
            Assert.NotEqual(Guid.Empty, identity!.Id);

            DiskIdentity? reread = new DiskIdentityProvider().GetOrCreate(root);
            Assert.Equal(identity.Id, reread!.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "disk-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
