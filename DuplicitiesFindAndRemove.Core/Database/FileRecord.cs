using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core.Database;

public sealed class FileRecordEntity : IFileRecord
{
    public long Id { get; set; }

    public Guid DiskId { get; set; }

    [MaxLength(ushort.MaxValue)]
    public string RelativePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public byte[]? SampleHash { get; set; }

    public byte[]? FullHash { get; set; }

    [NotMapped]
    public long? DuplicateOfFileId { get; set; }

    public ScanState State { get; set; } = ScanState.NotScanned;

    public long? ModificationTimeStamp { get; set; }

    public bool HasSampleHash => SampleHash is { Length: > 0 };

    public bool HasFullHash => FullHash is { Length: > 0 };

    public bool IsDuplicate => DuplicateOfFileId.HasValue;

    /// <summary>
    /// Portable location of the file (disk GUID + relative path), assembled from the two stored
    /// columns. The absolute path is not stored and must be reconstructed via the disk registry.
    /// </summary>
    [NotMapped]
    public FileLocation Location => new(DiskId, RelativePath);
}

public enum ScanState
{
    NotScanned = 0,
    SampleHashCalculated = 101,
    FullHashCalculated = 102,
    FileChanged = 201,
    FileDeleted = 300,
    ConfirmedDuplicate = 301,
    Canonical = 500
}
