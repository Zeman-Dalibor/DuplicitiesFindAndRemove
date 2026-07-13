using System;
using System.ComponentModel.DataAnnotations;

namespace DuplicitiesFindAndRemove.Core.Database;

public sealed class FileRecordEntity : IFileRecord
{
    public long Id { get; set; }

    [MaxLength(ushort.MaxValue)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? VolumeStableId { get; set; }

    [MaxLength(ushort.MaxValue)]
    public string? RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public byte[]? SampleHash { get; set; }

    public byte[]? FullHash { get; set; }

    public long? DuplicateOfFileId { get; set; }

    public ScanState State { get; set; } = ScanState.NotScanned;

    public long? ModificationTimeStamp { get; set; }

    public bool HasSampleHash => SampleHash is { Length: > 0 };

    public bool HasFullHash => FullHash is { Length: > 0 };

    public bool IsDuplicate => DuplicateOfFileId.HasValue;
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
