using DuplicitiesFindAndRemove.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DuplicitiesFindAndRemove.Core.Database;

public sealed class DuplicateDbContext : DbContext, IDuplicateIndex
{
    private readonly SqliteInMemoryDatabase? inMemoryDatabase;

    public DuplicateDbContext(DbContextOptions<DuplicateDbContext> options, SqliteInMemoryDatabase? inMemoryDatabase = null)
        : base(options)
    {
        this.inMemoryDatabase = inMemoryDatabase;
    }

    public void Initialize()
    {
        Database.EnsureCreated();
    }

    public DbSet<FileRecordEntity> FileRecords => Set<FileRecordEntity>();

    public DbSet<DuplicateRecordEntity> Duplicates => Set<DuplicateRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Canonical records and confirmed duplicates live in their own tables. Pruning duplicates
        // therefore never leaves gaps in the canonical "FileRecords" identity sequence.
        modelBuilder.Entity<FileRecordEntity>().ToTable("FileRecords");
        modelBuilder.Entity<DuplicateRecordEntity>().ToTable("Duplicates");
    }

    public async Task<FileRecordEntity?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var canonical = await FileRecords
            .FirstOrDefaultAsync(entity => entity.Path == path, cancellationToken);
        if (canonical is not null)
        {
            return canonical;
        }

        // A path already recorded as a duplicate is also "known" and must not be rescanned.
        var duplicate = await Duplicates
            .FirstOrDefaultAsync(entity => entity.Path == path, cancellationToken);

        return duplicate is null ? null : ToFileRecord(duplicate);
    }

    public async Task<IReadOnlyCollection<FileRecordEntity>> GetBySize(long sizeBytes, CancellationToken cancellationToken)
    {
        return await FileRecords
            .Where(entity => entity.SizeBytes == sizeBytes)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FileRecordEntity>> GetBySizeAndSampleHashAsync(long sizeBytes, byte[] sampleHash, CancellationToken cancellationToken = default)
    {
        return await FileRecords
            .Where(entity => entity.SizeBytes == sizeBytes && entity.SampleHash == sampleHash)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FileRecordEntity>> GetBySizeAndFullHashAsync(long sizeBytes, byte[] fullHash, CancellationToken cancellationToken)
    {
        return await FileRecords
            .Where(entity => entity.SizeBytes == sizeBytes && entity.FullHash == fullHash)
            .ToListAsync(cancellationToken);
    }

    public Task UpdateOrInsertAsync(FileRecordEntity record, CancellationToken cancellationToken = default)
        => UpsertCanonicalAndSaveAsync(record, cancellationToken);

    public void SetFileAsModified(FileRecordEntity record)
    {
        Entry(record).State = EntityState.Modified;
    }

    public async Task AddDuplicate(FileRecordEntity duplicate, CancellationToken cancellationToken)
    {
        if (duplicate.DuplicateOfFileId is null)
        {
            throw new ArgumentException("Duplicate record must reference a canonical file ID.", nameof(duplicate));
        }

        var existing = await Duplicates
            .FirstOrDefaultAsync(entity => entity.Path == duplicate.Path, cancellationToken);

        if (existing is null)
        {
            await Duplicates.AddAsync(ToDuplicateRecord(duplicate), cancellationToken);
        }
        else
        {
            existing.SizeBytes = duplicate.SizeBytes;
            existing.VolumeStableId = duplicate.VolumeStableId;
            existing.RelativePath = duplicate.RelativePath;
            existing.SampleHash = duplicate.SampleHash;
            existing.FullHash = duplicate.FullHash;
            existing.DuplicateOfFileId = duplicate.DuplicateOfFileId ?? throw new ArgumentException("Duplicate record must reference a canonical file ID.", nameof(duplicate));
            existing.State = duplicate.State;
            existing.ModificationTimeStamp = duplicate.ModificationTimeStamp;
        }

        await SaveChangesAsync(cancellationToken);
    }

    public Task AddCanonical(FileRecordEntity record, CancellationToken cancellationToken)
        => UpsertCanonicalAndSaveAsync(record, cancellationToken);

    // Persists tracked changes to the in-memory database and then backs the in-memory database up
    // to the on-disk file, so progress survives an interrupted scan.
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await SaveChangesAsync(cancellationToken);
        inMemoryDatabase?.Persist();
    }

    // Inserts or updates a canonical record and persists immediately. Persisting per record assigns
    // the database-generated Id, which the scanner relies on when linking duplicates to their
    // canonical record. The operation is idempotent: a record that is already tracked is not
    // inserted twice.
    private async Task UpsertCanonicalAndSaveAsync(FileRecordEntity record, CancellationToken cancellationToken)
    {
        if (record.DuplicateOfFileId is not null)
        {
            throw new ArgumentException("Canonical record must not reference a canonical file ID.", nameof(record));
        }

        if (Entry(record).State == EntityState.Detached)
        {
            var existing = await FileRecords
                .FirstOrDefaultAsync(entity => entity.Path == record.Path, cancellationToken);

            if (existing is null)
            {
                await FileRecords.AddAsync(record, cancellationToken);
            }
            else if (!ReferenceEquals(existing, record))
            {
                Entry(existing).CurrentValues.SetValues(record);
            }
        }

        await SaveChangesAsync(cancellationToken);
    }

    private static DuplicateRecordEntity ToDuplicateRecord(FileRecordEntity source) => new()
    {
        Path = source.Path,
        VolumeStableId = source.VolumeStableId,
        RelativePath = source.RelativePath,
        SizeBytes = source.SizeBytes,
        SampleHash = source.SampleHash,
        FullHash = source.FullHash,
        DuplicateOfFileId = source.DuplicateOfFileId ?? throw new ArgumentException("Duplicate record must reference a canonical file ID.", nameof(source)),
        State = source.State,
        ModificationTimeStamp = source.ModificationTimeStamp
    };

    private static FileRecordEntity ToFileRecord(DuplicateRecordEntity source) => new()
    {
        Path = source.Path,
        VolumeStableId = source.VolumeStableId,
        RelativePath = source.RelativePath,
        SizeBytes = source.SizeBytes,
        SampleHash = source.SampleHash,
        FullHash = source.FullHash,
        DuplicateOfFileId = source.DuplicateOfFileId,
        State = source.State,
        ModificationTimeStamp = source.ModificationTimeStamp
    };
}
