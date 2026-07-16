using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

/// <summary>
/// Maps disk GUIDs to their current mount root, so a stored <see cref="FileLocation"/>
/// (disk GUID + relative path) can be turned back into an absolute path on this machine.
/// The mapping is built once at startup by inspecting the mounted disks.
/// </summary>
public interface IDiskRegistry
{
    /// <summary>
    /// Returns the current absolute root of the disk with the given GUID, or <c>null</c> when the
    /// disk is not currently mounted on this machine.
    /// </summary>
    string? TryGetDiskRoot(Guid diskId);

    /// <summary>
    /// Reconstructs the absolute path of a file from its portable location, or <c>null</c> when the
    /// file's disk is not currently mounted.
    /// </summary>
    string? TryGetAbsolutePath(FileLocation location);
}
