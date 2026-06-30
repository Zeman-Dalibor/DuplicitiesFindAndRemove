using DuplicitiesFindAndRemove.Core.Hashing;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class Blake3HasherTests : IDisposable
{
    private readonly string directory;
    private readonly Blake3Hasher hasher = new();

    public Blake3HasherTests()
    {
        directory = Path.Combine(Path.GetTempPath(), "blake3-tests", Guid.NewGuid().ToString("N"));
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
    public async Task ComputeFullHash_IsDeterministic_ForSameContent()
    {
        string a = CreateFile("a.bin", new byte[] { 1, 2, 3, 4 });
        string b = CreateFile("b.bin", new byte[] { 1, 2, 3, 4 });

        byte[] hashA = await hasher.ComputeFullHashAsync(a);
        byte[] hashB = await hasher.ComputeFullHashAsync(b);

        Assert.Equal(hashA, hashB);
        Assert.Equal(32, hashA.Length);
    }

    [Fact]
    public async Task ComputeFullHash_Differs_ForDifferentContent()
    {
        string a = CreateFile("a.bin", new byte[] { 1, 2, 3, 4 });
        string b = CreateFile("b.bin", new byte[] { 4, 3, 2, 1 });

        byte[] hashA = await hasher.ComputeFullHashAsync(a);
        byte[] hashB = await hasher.ComputeFullHashAsync(b);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public async Task ComputeSampleHash_IsDeterministic_ForSameContent()
    {
        byte[] content = Enumerable.Range(0, 300 * 1024).Select(i => (byte)i).ToArray();
        string a = CreateFile("a.bin", content);
        string b = CreateFile("b.bin", content);

        byte[] hashA = await hasher.ComputeSampleHashAsync(a);
        byte[] hashB = await hasher.ComputeSampleHashAsync(b);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public async Task ComputeSampleHash_IgnoresMiddleBytes_ForLargeFiles()
    {
        // Large files are sampled at the head and tail only, so a change in the middle is not
        // reflected in the sample hash. The full hash still differs, which is why the scanner
        // always falls back to a byte-by-byte comparison.
        const int size = 300 * 1024;
        byte[] first = new byte[size];
        byte[] second = new byte[size];
        second[size / 2] = 0xFF;

        string a = CreateFile("a.bin", first);
        string b = CreateFile("b.bin", second);

        Assert.Equal(await hasher.ComputeSampleHashAsync(a), await hasher.ComputeSampleHashAsync(b));
        Assert.NotEqual(await hasher.ComputeFullHashAsync(a), await hasher.ComputeFullHashAsync(b));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ComputeFullHash_Throws_ForInvalidPath(string? path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => hasher.ComputeFullHashAsync(path!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ComputeSampleHash_Throws_ForInvalidPath(string? path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => hasher.ComputeSampleHashAsync(path!));
    }
}
