namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Portable identity of a disk, backed by a placeholder file stored in the disk root.
/// Because the identity travels with the file system content, it stays stable regardless of
/// drive letter or mount point and is safe to persist in the database.
/// </summary>
public sealed class DiskIdentity
{
    public DiskIdentity(Guid id, string? label)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Disk identity GUID must not be empty.", nameof(id));
        }

        Id = id;
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    /// <summary>
    /// Globally unique identifier of the disk.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Optional human-readable label of the disk (e.g. its volume label).
    /// </summary>
    public string? Label { get; }
}
