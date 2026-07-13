namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Volume identity and relative location of a file on that volume.
/// </summary>
public sealed class VolumePathInfo
{
    public VolumePathInfo(string volumeStableId, string relativePath, string volumeRootPath)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(volumeStableId);
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(volumeRootPath);

        VolumeStableId = volumeStableId;
        RelativePath = relativePath;
        VolumeRootPath = volumeRootPath;
    }

    /// <summary>
    /// Cross-platform stable volume identifier, e.g. <c>partuuid:...</c> or <c>volserial:...</c>.
    /// </summary>
    public string VolumeStableId { get; }

    /// <summary>
    /// Path relative to the volume root, always using forward slashes.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Absolute path of the volume mount point or drive root at resolution time.
    /// </summary>
    public string VolumeRootPath { get; }
}
