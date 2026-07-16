namespace DuplicitiesFindAndRemove.Core.Database;

using DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Common file metadata shared by the canonical <see cref="FileRecordEntity"/> and the
/// <see cref="DuplicateRecordEntity"/>. The two entities map to separate tables but expose the
/// same fields, so code that only reads or copies that metadata can work against this interface.
/// A file is located by its portable pair (<see cref="DiskId"/>, <see cref="RelativePath"/>); the
/// absolute path is never stored.
/// </summary>
public interface IFileRecord
{
    long Id { get; set; }

    Guid DiskId { get; set; }

    string RelativePath { get; set; }

    long SizeBytes { get; set; }

    byte[]? SampleHash { get; set; }

    byte[]? FullHash { get; set; }

    ScanState State { get; set; }

    long? ModificationTimeStamp { get; set; }

    /// <summary>
    /// Portable location of the file (disk GUID + relative path), assembled from the two stored
    /// columns. The absolute path is not stored and must be reconstructed via the disk registry.
    /// </summary>
    FileLocation Location { get; }
}
