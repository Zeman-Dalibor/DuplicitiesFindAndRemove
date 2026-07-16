using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

/// <summary>
/// Provides the portable identity of a disk based on a placeholder file kept in the disk root.
/// </summary>
public interface IDiskIdentityProvider
{
    /// <summary>
    /// Ensures the placeholder identity file exists in <paramref name="diskRootPath"/>, creating it
    /// with a freshly generated GUID when missing, and returns the resulting disk identity.
    /// Throws when the identity cannot be established (for example when the placeholder file cannot
    /// be created in a read-only disk root). There is intentionally no fallback identity.
    /// </summary>
    DiskIdentity GetOrCreate(string diskRootPath);
}
