namespace DuplicitiesFindAndRemove.Core.Database;

/// <summary>
/// Common file metadata shared by the canonical <see cref="FileRecordEntity"/> and the
/// <see cref="DuplicateRecordEntity"/>. The two entities map to separate tables but expose the
/// same fields, so code that only reads or copies that metadata can work against this interface.
/// </summary>
public interface IFileRecord
{
    long Id { get; set; }

    string Path { get; set; }

    string? VolumeStableId { get; set; }

    string? RelativePath { get; set; }

    long SizeBytes { get; set; }

    byte[]? SampleHash { get; set; }

    byte[]? FullHash { get; set; }

    ScanState State { get; set; }

    long? ModificationTimeStamp { get; set; }
}
