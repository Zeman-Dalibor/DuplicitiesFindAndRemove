using System.ComponentModel.DataAnnotations;

namespace DuplicitiesFindAndRemove.Core.Database;

/// <summary>
/// A confirmed duplicate file, stored in its own table separate from the canonical
/// <see cref="FileRecordEntity"/> records. Keeping duplicates apart means that pruning
/// (deleting) them does not fragment the identity sequence of the canonical index.
/// </summary>
public sealed class DuplicateRecordEntity : IFileRecord
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

    /// <summary>
    /// Identity of the canonical <see cref="FileRecordEntity"/> this file duplicates.
    /// </summary>
    public long DuplicateOfFileId { get; set; }

    public ScanState State { get; set; } = ScanState.ConfirmedDuplicate;

    public long? ModificationTimeStamp { get; set; }
}
