namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Portable location of a file: the GUID of the disk it lives on plus the path relative to that
/// disk's root. Together these two values identify a file independently of drive letter or mount
/// point, so they can be stored and compared across machines and operating systems.
/// Relative paths always use forward slashes and are compared case-sensitively (ordinal).
/// </summary>
public sealed class FileLocation : IEquatable<FileLocation>, IComparable<FileLocation>
{
    public FileLocation(Guid diskId, string relativePath)
    {
        if (diskId == Guid.Empty)
        {
            throw new ArgumentException("Disk id must not be empty.", nameof(diskId));
        }

        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(relativePath);

        DiskId = diskId;
        RelativePath = relativePath;
    }

    /// <summary>
    /// GUID of the disk the file lives on, taken from the disk's placeholder identity file.
    /// </summary>
    public Guid DiskId { get; }

    /// <summary>
    /// Path relative to the disk root, always using forward slashes.
    /// </summary>
    public string RelativePath { get; }

    public bool Equals(FileLocation? other)
        => other is not null
           && DiskId.Equals(other.DiskId)
           && string.Equals(RelativePath, other.RelativePath, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as FileLocation);

    public override int GetHashCode()
        => HashCode.Combine(DiskId, StringComparer.Ordinal.GetHashCode(RelativePath));

    public int CompareTo(FileLocation? other)
    {
        if (other is null)
        {
            return 1;
        }

        int byDisk = DiskId.CompareTo(other.DiskId);
        return byDisk != 0 ? byDisk : string.CompareOrdinal(RelativePath, other.RelativePath);
    }

    public override string ToString() => $"{DiskId:D}:{RelativePath}";

    public static bool operator ==(FileLocation? left, FileLocation? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(FileLocation? left, FileLocation? right)
        => !(left == right);

    public static bool operator <(FileLocation? left, FileLocation? right)
        => Comparer<FileLocation>.Default.Compare(left, right) < 0;

    public static bool operator >(FileLocation? left, FileLocation? right)
        => Comparer<FileLocation>.Default.Compare(left, right) > 0;

    public static bool operator <=(FileLocation? left, FileLocation? right)
        => Comparer<FileLocation>.Default.Compare(left, right) <= 0;

    public static bool operator >=(FileLocation? left, FileLocation? right)
        => Comparer<FileLocation>.Default.Compare(left, right) >= 0;
}
