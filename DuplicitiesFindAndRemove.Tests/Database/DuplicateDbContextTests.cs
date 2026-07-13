using DuplicitiesFindAndRemove.Core.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class DuplicateDbContextTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<DuplicateDbContext> options;

    public DuplicateDbContextTests()
    {
        // A shared open connection keeps the in-memory database alive across contexts.
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

    [Fact]
    public async Task GetByPathAsync_ReturnsRecord_WhenPresent()
    {
        await using (var context = CreateContext())
        {
            await context.AddCanonical(new FileRecordEntity { Path = "C:\\files\\a.txt", SizeBytes = 10 }, default);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var found = await context.GetByPathAsync("C:\\files\\a.txt");

            Assert.NotNull(found);
            Assert.Equal(10, found!.SizeBytes);
        }
    }

    [Fact]
    public async Task GetByPathAsync_ReturnsNull_WhenMissing()
    {
        await using var context = CreateContext();

        Assert.Null(await context.GetByPathAsync("C:\\missing.txt"));
    }

    [Fact]
    public async Task UpdateOrInsertAsync_InsertsNewRecord()
    {
        await using (var context = CreateContext())
        {
            await context.UpdateOrInsertAsync(new FileRecordEntity { Path = "C:\\files\\a.txt", SizeBytes = 1 });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            Assert.Single(context.FileRecords);
        }
    }

    [Fact]
    public async Task UpdateOrInsertAsync_UpdatesExistingRecord()
    {
        long id;
        await using (var context = CreateContext())
        {
            var record = new FileRecordEntity { Path = "C:\\files\\a.txt", SizeBytes = 1 };
            await context.AddCanonical(record, default);
            await context.SaveChangesAsync();
            id = record.Id;
        }

        await using (var context = CreateContext())
        {
            await context.UpdateOrInsertAsync(new FileRecordEntity
            {
                Id = id,
                Path = "C:\\files\\a.txt",
                SizeBytes = 999,
                State = ScanState.Canonical
            });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var record = Assert.Single(context.FileRecords);
            Assert.Equal(999, record.SizeBytes);
            Assert.Equal(ScanState.Canonical, record.State);
        }
    }

    [Fact]
    public async Task GetBySizeAndFullHashAsync_ReturnsMatchingRecord()
    {
        byte[] hash = { 1, 2, 3, 4 };
        await using (var context = CreateContext())
        {
            await context.AddCanonical(new FileRecordEntity { Path = "C:\\a", SizeBytes = 100, FullHash = hash }, default);
            await context.AddCanonical(new FileRecordEntity { Path = "C:\\b", SizeBytes = 100, FullHash = new byte[] { 9 } }, default);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var matches = await context.GetBySizeAndFullHashAsync(100, hash, default);

            var match = Assert.Single(matches);
            Assert.Equal("C:\\a", match.Path);
        }
    }

    [Fact]
    public async Task GetBySizeAndSampleHashAsync_ReturnsMatchingRecord()
    {
        byte[] hash = { 5, 6, 7 };
        await using (var context = CreateContext())
        {
            await context.AddCanonical(new FileRecordEntity { Path = "C:\\a", SizeBytes = 200, SampleHash = hash }, default);
            await context.AddCanonical(new FileRecordEntity { Path = "C:\\b", SizeBytes = 999, SampleHash = hash }, default);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var matches = await context.GetBySizeAndSampleHashAsync(200, hash);

            var match = Assert.Single(matches);
            Assert.Equal("C:\\a", match.Path);
        }
    }

    [Fact]
    public async Task AddCanonical_PersistsVolumePathFields()
    {
        await using (var context = CreateContext())
        {
            await context.AddCanonical(new FileRecordEntity
            {
                Path = "C:\\files\\a.txt",
                VolumeStableId = "partuuid:abc-123",
                RelativePath = "files/a.txt",
                SizeBytes = 10
            }, default);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var found = await context.GetByPathAsync("C:\\files\\a.txt");

            Assert.NotNull(found);
            Assert.Equal("partuuid:abc-123", found!.VolumeStableId);
            Assert.Equal("files/a.txt", found.RelativePath);
        }
    }
}
