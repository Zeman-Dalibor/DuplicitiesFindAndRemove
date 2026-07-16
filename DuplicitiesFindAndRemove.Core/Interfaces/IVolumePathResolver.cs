using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IVolumePathResolver
{
    /// <summary>
    /// Resolves a file path into its portable <see cref="FileLocation"/> (disk GUID + relative path).
    /// The disk GUID comes from the placeholder identity file in the disk root; if that file cannot
    /// be created or read, resolution throws (there is intentionally no fallback identity).
    /// </summary>
    FileLocation Resolve(string filePath);
}
