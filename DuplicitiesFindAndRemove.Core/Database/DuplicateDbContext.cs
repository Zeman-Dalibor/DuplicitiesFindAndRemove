using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Volume;
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
        modelBuilder.Entity<FileRecordEntity>(entity =>
        {
            entity.ToTable("FileRecords");

            // Indexes backing the lookups below. Without them every candidate query does a full
            // table scan, which degrades to quadratic complexity on large file sets.
            entity.HasIndex(record => new { record.DiskId, record.RelativePath });
            entity.HasIndex(record => record.SizeBytes);
            entity.HasIndex(record => new { record.SizeBytes, record.SampleHash });
            entity.HasIndex(record => new { record.SizeBytes, record.FullHash });
        });

        modelBuilder.Entity<DuplicateRecordEntity>(entity =>
        {
            entity.ToTable("Duplicates");

            // Duplicate lookups are keyed by location when deciding whether a file is already known.
            entity.HasIndex(record => new { record.DiskId, record.RelativePath });
        });
    }

    public async Task<FileRecordEntity?> GetByLocationAsync(FileLocation location, CancellationToken cancellationToken = default)
    {
        Guid diskId = location.DiskId;
        string relativePath = location.RelativePath;

        var canonical = await FileRecords
            .FirstOrDefaultAsync(entity => entity.DiskId == diskId && entity.RelativePath == relativePath, cancellationToken);
        if (canonical is not null)
        {
            return canonical;
        }

        // A location already recorded as a duplicate is also "known" and must not be rescanned.
        var duplicate = await Duplicates
            .FirstOrDefaultAsync(entity => entity.DiskId == diskId && entity.RelativePath == relativePath, cancellationToken);

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
            .FirstOrDefaultAsync(entity => entity.DiskId == duplicate.DiskId && entity.RelativePath == duplicate.RelativePath, cancellationToken);

        if (existing is null)
        {
            await Duplicates.AddAsync(ToDuplicateRecord(duplicate), cancellationToken);
        }
        else
        {
            existing.SizeBytes = duplicate.SizeBytes;
            existing.DiskId = duplicate.DiskId;
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

    public async Task<IReadOnlyList<FileRecordEntity>> GetCanonicalRecordsUnderLocationAsync(
        FileLocation directoryLocation,
        CancellationToken cancellationToken = default)
    {
        string directoryRelativePath = directoryLocation.RelativePath;
        Guid diskId = directoryLocation.DiskId;

        return await FileRecords
            .Where(record => record.DiskId == diskId
                && (directoryRelativePath.Length == 0
                    || record.RelativePath == directoryRelativePath
                    || record.RelativePath.StartsWith(directoryRelativePath)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DuplicateRecordEntity>> GetDuplicateRecordsUnderLocationAsync(
        FileLocation directoryLocation,
        CancellationToken cancellationToken = default)
    {
        string directoryRelativePath = directoryLocation.RelativePath;
        Guid diskId = directoryLocation.DiskId;

        return await Duplicates
            .Where(record => record.DiskId == diskId
                && (directoryRelativePath.Length == 0
                    || record.RelativePath == directoryRelativePath
                    || record.RelativePath.StartsWith(directoryRelativePath)))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteCanonicalWithDuplicatesAsync(long canonicalId, CancellationToken cancellationToken = default)
    {
        List<DuplicateRecordEntity> linkedDuplicates = await Duplicates
            .Where(duplicate => duplicate.DuplicateOfFileId == canonicalId)
            .ToListAsync(cancellationToken);

        if (linkedDuplicates.Count > 0)
        {
            Duplicates.RemoveRange(linkedDuplicates);
        }

        FileRecordEntity? canonical = await FileRecords.FindAsync(new object[] { canonicalId }, cancellationToken);
        if (canonical is not null)
        {
            FileRecords.Remove(canonical);
        }

        await SaveChangesAsync(cancellationToken);
        return linkedDuplicates.Count;
    }

    public async Task DeleteDuplicateAsync(long duplicateId, CancellationToken cancellationToken = default)
    {
        DuplicateRecordEntity? duplicate = await Duplicates.FindAsync(new object[] { duplicateId }, cancellationToken);
        if (duplicate is null)
        {
            return;
        }

        Duplicates.Remove(duplicate);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDuplicateAsync(DuplicateRecordEntity duplicate, CancellationToken cancellationToken = default)
    {
        Duplicates.Update(duplicate);
        await SaveChangesAsync(cancellationToken);
    }

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
                .FirstOrDefaultAsync(entity => entity.DiskId == record.DiskId && entity.RelativePath == record.RelativePath, cancellationToken);

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
        DiskId = source.DiskId,
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
        DiskId = source.DiskId,
        RelativePath = source.RelativePath,
        SizeBytes = source.SizeBytes,
        SampleHash = source.SampleHash,
        FullHash = source.FullHash,
        DuplicateOfFileId = source.DuplicateOfFileId,
        State = source.State,
        ModificationTimeStamp = source.ModificationTimeStamp
    };
}
