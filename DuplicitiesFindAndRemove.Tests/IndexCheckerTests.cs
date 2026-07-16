using DuplicitiesFindAndRemove.Core;
using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Volume;
using DuplicitiesFindAndRemove.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class IndexCheckerTests : IDisposable
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "index-checker-tests");

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<DuplicateDbContext> options;

    public IndexCheckerTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        options = new DbContextOptionsBuilder<DuplicateDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        connection.Dispose();
    }

    private DuplicateDbContext CreateContext() => new(options);

    private static string PathFor(string name) => Path.Combine(Root, name);

    private static IndexChecker CreateChecker(
        InMemoryFileSystem fileSystem,
        DuplicateDbContext context)
    {
        return new IndexChecker(
            fileSystem,
            new FakeContentHasher(fileSystem),
            context,
            new FakeVolumePathResolver(),
            new FakeDiskRegistry());
    }

    private static async Task<FileRecordEntity> AddCanonicalAsync(
        DuplicateDbContext context,
        string relativePath,
        long sizeBytes,
        byte[]? fullHash = null,
        byte[]? sampleHash = null,
        long? mtime = 0)
    {
        var location = new FakeVolumePathResolver().Resolve(PathFor(relativePath));
        var record = new FileRecordEntity
        {
            DiskId = location.DiskId,
            RelativePath = location.RelativePath,
            SizeBytes = sizeBytes,
            FullHash = fullHash,
            SampleHash = sampleHash,
            ModificationTimeStamp = mtime,
            State = fullHash is not null ? ScanState.FullHashCalculated : ScanState.SampleHashCalculated
        };

        await context.AddCanonical(record, default);
        await context.SaveChangesAsync();
        return record;
    }

    private static async Task<DuplicateRecordEntity> AddDuplicateAsync(
        DuplicateDbContext context,
        string relativePath,
        long canonicalId,
        long sizeBytes,
        long? mtime = 0)
    {
        var location = new FakeVolumePathResolver().Resolve(PathFor(relativePath));
        var duplicate = new FileRecordEntity
        {
            DiskId = location.DiskId,
            RelativePath = location.RelativePath,
            SizeBytes = sizeBytes,
            ModificationTimeStamp = mtime,
            DuplicateOfFileId = canonicalId,
            State = ScanState.ConfirmedDuplicate
        };

        await context.AddDuplicate(duplicate, default);
        return (await context.Duplicates.SingleAsync(record => record.RelativePath == location.RelativePath));
    }

    [Fact]
    public async Task CheckAsync_DeletesCanonical_WhenFileMissing()
    {
        await using var context = CreateContext();
        await AddCanonicalAsync(context, "missing.txt", 10);

        var result = await CreateChecker(new InMemoryFileSystem(), context).CheckAsync(Root);

        Assert.Equal(1, result.DeletedCanonicalRecordsCount);
        Assert.Empty(context.FileRecords);
    }

    [Fact]
    public async Task CheckAsync_DeletesDuplicate_WhenFileMissing()
    {
        await using var context = CreateContext();
        await AddCanonicalAsync(context, "original.txt", 10);
        var canonical = await context.FileRecords.SingleAsync();
        await AddDuplicateAsync(context, "copy.txt", canonical.Id, 10);

        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("original.txt"), "same-content");

        var result = await CreateChecker(fileSystem, context).CheckAsync(Root);

        Assert.Equal(1, result.DeletedDuplicateRecordsCount);
        Assert.Single(context.FileRecords);
        Assert.Empty(context.Duplicates);
    }

    [Fact]
    public async Task CheckAsync_CascadeDeletesDuplicates_WhenCanonicalMissing()
    {
        await using var context = CreateContext();
        await AddCanonicalAsync(context, "original.txt", 10);
        var canonical = await context.FileRecords.SingleAsync(record => record.RelativePath.EndsWith("original.txt"));
        await AddDuplicateAsync(context, "copy.txt", canonical.Id, 10);
        await AddDuplicateAsync(context, "other/copy.txt", canonical.Id, 10);

        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("copy.txt"), "same-content")
            .AddFile(PathFor("other/copy.txt"), "same-content");

        var result = await CreateChecker(fileSystem, context).CheckAsync(Root);

        Assert.Equal(1, result.DeletedCanonicalRecordsCount);
        Assert.Equal(2, result.CascadeDeletedDuplicatesCount);
        Assert.Empty(context.FileRecords);
        Assert.Empty(context.Duplicates);
    }

    [Fact]
    public async Task CheckAsync_RecalculatesFullHash_WhenSmallFileChanged()
    {
        await using var context = CreateContext();
        byte[] oldHash = { 1, 2, 3 };
        await AddCanonicalAsync(context, "small.txt", 5, fullHash: oldHash);

        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("small.txt"), "changed!");

        var result = await CreateChecker(fileSystem, context).CheckAsync(Root);

        Assert.Equal(1, result.UpdatedRecordsCount);
        var record = await context.FileRecords.SingleAsync();
        Assert.Equal(8, record.SizeBytes);
        Assert.NotNull(record.FullHash);
        Assert.NotEqual(oldHash, record.FullHash);
        Assert.Null(record.SampleHash);
        Assert.Equal(ScanState.FullHashCalculated, record.State);
    }

    [Fact]
    public async Task CheckAsync_RecalculatesSampleHash_WhenLargeFileMtimeChanged()
    {
        await using var context = CreateContext();
        byte[] oldSampleHash = { 9, 9, 9 };
        byte[] oldFullHash = { 1, 1, 1 };
        long largeSize = DuplicateDetectionOptions.SmallFileFullHashThresholdBytes + 1;

        await AddCanonicalAsync(
            context,
            "large.bin",
            largeSize,
            fullHash: oldFullHash,
            sampleHash: oldSampleHash,
            mtime: 100);

        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("large.bin"), new byte[largeSize])
            .WithLastWriteTimeUtcNanoseconds(PathFor("large.bin"), 200);

        var result = await CreateChecker(fileSystem, context).CheckAsync(Root);

        Assert.Equal(1, result.UpdatedRecordsCount);
        var record = await context.FileRecords.SingleAsync();
        Assert.Equal(largeSize, record.SizeBytes);
        Assert.Equal(200, record.ModificationTimeStamp);
        Assert.NotNull(record.SampleHash);
        Assert.NotEqual(oldSampleHash, record.SampleHash);
        Assert.Null(record.FullHash);
        Assert.Equal(ScanState.SampleHashCalculated, record.State);
    }

    [Fact]
    public async Task CheckAsync_DoesNotTouchRecords_OutsideFilterDirectory()
    {
        await using var context = CreateContext();
        await AddCanonicalAsync(context, "inside/missing.txt", 10);
        await AddCanonicalAsync(context, "outside/keep.txt", 10);

        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("outside/keep.txt"), "keep");

        var result = await CreateChecker(fileSystem, context).CheckAsync(PathFor("inside"));

        Assert.Equal(1, result.DeletedCanonicalRecordsCount);
        Assert.Single(context.FileRecords);
        Assert.Contains(context.FileRecords, record => record.RelativePath.EndsWith("outside/keep.txt"));
        Assert.DoesNotContain(context.FileRecords, record => record.RelativePath.EndsWith("inside/missing.txt"));
    }

    [Fact]
    public async Task CheckAsync_LeavesUnchanged_WhenMetadataMatches()
    {
        await using var context = CreateContext();
        byte[] hash = { 4, 4, 4 };
        await AddCanonicalAsync(context, "stable.txt", 6, fullHash: hash, mtime: 0);

        var fileSystem = new InMemoryFileSystem()
            .AddFile(PathFor("stable.txt"), "stable");

        var result = await CreateChecker(fileSystem, context).CheckAsync(Root);

        Assert.Equal(1, result.UnchangedRecordsCount);
        Assert.Equal(0, result.UpdatedRecordsCount);
        var record = await context.FileRecords.SingleAsync();
        Assert.Equal(hash, record.FullHash);
    }
}
