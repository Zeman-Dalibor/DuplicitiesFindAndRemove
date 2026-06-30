using DuplicitiesFindAndRemove.Core;
using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Tests.Fakes;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class DuplicateScannerTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "scanner-tests");

    private static string PathFor(string name) => Path.Combine(Root, name);

    private static DuplicateScanner CreateScanner(
        InMemoryFileSystem fileSystem,
        InMemoryDuplicateIndex index,
        DuplicateDetectionOptions? options = null)
    {
        return new DuplicateScanner(
            fileSystem,
            new FakeContentHasher(fileSystem),
            index,
            new FakeDuplicateVerifier(fileSystem),
            options ?? new DuplicateDetectionOptions());
    }

    [Fact]
    public async Task ScanAsync_ReturnsNoDuplicates_ForUniqueFiles()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("a.txt"), "aaaa")
            .AddFile(PathFor("b.txt"), "bbbb");
        var index = new InMemoryDuplicateIndex();

        var result = await CreateScanner(fileSystem, index).ScanAsync(Root);

        Assert.Empty(result.ConfirmedDuplicates);
        Assert.Equal(2, index.Records.Count);
    }

    [Fact]
    public async Task ScanAsync_DetectsDuplicate_ForIdenticalSmallFiles()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("original.txt"), "same-content")
            .AddFile(PathFor("copy.txt"), "same-content");
        var index = new InMemoryDuplicateIndex();

        var result = await CreateScanner(fileSystem, index).ScanAsync(Root);

        var duplicate = Assert.Single(result.ConfirmedDuplicates);
        Assert.Equal(ScanState.ConfirmedDuplicate, duplicate.State);
        Assert.NotNull(duplicate.DuplicateOfFileId);

        var canonical = index.Records.Single(record => record.Id == duplicate.DuplicateOfFileId);
        Assert.Equal(ScanState.Canonical, canonical.State);
    }

    [Fact]
    public async Task ScanAsync_DetectsDuplicate_ForIdenticalLargeFiles()
    {
        // A small threshold forces these files through the sample-hash (large file) path.
        var options = new DuplicateDetectionOptions { SmallFileFullHashThresholdBytes = 4 };
        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("original.bin"), "large-content")
            .AddFile(PathFor("copy.bin"), "large-content");
        var index = new InMemoryDuplicateIndex();

        var result = await CreateScanner(fileSystem, index, options).ScanAsync(Root);

        Assert.Single(result.ConfirmedDuplicates);
    }

    [Fact]
    public async Task ScanAsync_DoesNotConfirm_WhenSampleHashCollidesButContentDiffers()
    {
        // Same size means the same sample hash in the fake hasher, but the content differs,
        // so the byte-by-byte verification must reject the duplicate.
        var options = new DuplicateDetectionOptions { SmallFileFullHashThresholdBytes = 4 };
        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("a.bin"), "AAAAAAAAAAAA")
            .AddFile(PathFor("b.bin"), "BBBBBBBBBBBB");
        var index = new InMemoryDuplicateIndex();

        var result = await CreateScanner(fileSystem, index, options).ScanAsync(Root);

        Assert.Empty(result.ConfirmedDuplicates);
        Assert.Equal(2, index.Records.Count);
    }

    [Fact]
    public async Task ScanAsync_SkipsFile_AlreadyInIndex()
    {
        string path = PathFor("existing.txt");
        var fileSystem = new InMemoryFileSystem().AddFile(path, "content");
        var index = new InMemoryDuplicateIndex();
        await index.AddCanonical(new FileRecordEntity { Path = Path.GetFullPath(path), SizeBytes = 7 }, default);

        var result = await CreateScanner(fileSystem, index).ScanAsync(Root);

        Assert.Empty(result.ConfirmedDuplicates);
        Assert.Single(index.Records);
    }

    [Fact]
    public async Task ScanAsync_Throws_WhenCancelled()
    {
        var fileSystem = new InMemoryFileSystem().AddFile(PathFor("a.txt"), "x");
        var index = new InMemoryDuplicateIndex();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateScanner(fileSystem, index).ScanAsync(Root, cts.Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ScanAsync_Throws_ForInvalidRootPath(string? rootPath)
    {
        var scanner = CreateScanner(new InMemoryFileSystem(), new InMemoryDuplicateIndex());

        await Assert.ThrowsAsync<ArgumentException>(() => scanner.ScanAsync(rootPath!));
    }

    [Fact]
    public void Constructor_Throws_ForNullDependencies()
    {
        var fileSystem = new InMemoryFileSystem();
        var hasher = new FakeContentHasher(fileSystem);
        var index = new InMemoryDuplicateIndex();
        var verifier = new FakeDuplicateVerifier(fileSystem);
        var options = new DuplicateDetectionOptions();

        Assert.Throws<ArgumentNullException>(() => new DuplicateScanner(null!, hasher, index, verifier, options));
        Assert.Throws<ArgumentNullException>(() => new DuplicateScanner(fileSystem, null!, index, verifier, options));
        Assert.Throws<ArgumentNullException>(() => new DuplicateScanner(fileSystem, hasher, null!, verifier, options));
        Assert.Throws<ArgumentNullException>(() => new DuplicateScanner(fileSystem, hasher, index, null!, options));
        Assert.Throws<ArgumentNullException>(() => new DuplicateScanner(fileSystem, hasher, index, verifier, null!));
    }

    [Fact]
    public void Constructor_Throws_ForInvalidOptions()
    {
        var fileSystem = new InMemoryFileSystem();
        var options = new DuplicateDetectionOptions { SmallFileFullHashThresholdBytes = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new DuplicateScanner(
            fileSystem,
            new FakeContentHasher(fileSystem),
            new InMemoryDuplicateIndex(),
            new FakeDuplicateVerifier(fileSystem),
            options));
    }
}
