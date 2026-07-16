using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Verification;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class ByteCompareVerifierTests : IDisposable
{
    private readonly string directory;
    private readonly ByteCompareVerifier verifier = new();

    public ByteCompareVerifierTests()
    {
        directory = Path.Combine(Path.GetTempPath(), "bytecompare-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string CreateFile(string name, byte[] content)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public async Task HasSameContent_ReturnsTrue_ForIdenticalFiles()
    {
        string a = CreateFile("a.bin", new byte[] { 1, 2, 3 });
        string b = CreateFile("b.bin", new byte[] { 1, 2, 3 });

        Assert.True(await verifier.HasSameContentAsync(a, b));
    }

    [Fact]
    public async Task HasSameContent_ReturnsFalse_ForSameLengthDifferentContent()
    {
        string a = CreateFile("a.bin", new byte[] { 1, 2, 3 });
        string b = CreateFile("b.bin", new byte[] { 1, 2, 4 });

        Assert.False(await verifier.HasSameContentAsync(a, b));
    }

    [Fact]
    public async Task HasSameContent_ReturnsFalse_ForDifferentLength()
    {
        string a = CreateFile("a.bin", new byte[] { 1, 2, 3 });
        string b = CreateFile("b.bin", new byte[] { 1, 2 });

        Assert.False(await verifier.HasSameContentAsync(a, b));
    }

    [Fact]
    public async Task HasSameContent_ReturnsFalse_WhenFileMissing()
    {
        string a = CreateFile("a.bin", new byte[] { 1 });
        string missing = Path.Combine(directory, "missing.bin");

        Assert.False(await verifier.HasSameContentAsync(a, missing));
    }

    [Fact]
    public async Task HasSameContent_Throws_ForSamePath()
    {
        string a = CreateFile("a.bin", new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => verifier.HasSameContentAsync(a, a));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HasSameContent_Throws_ForInvalidPath(string? path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => verifier.HasSameContentAsync(path!, "other.bin"));
    }
}
