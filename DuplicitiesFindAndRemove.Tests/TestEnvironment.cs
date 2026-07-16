namespace DuplicitiesFindAndRemove.Tests;

/// <summary>
/// Helpers describing the capabilities of the environment the tests run in.
/// </summary>
internal static class TestEnvironment
{
    /// <summary>
    /// Returns whether a file can be created in the root of the drive that hosts the given path.
    /// The disk-identity placeholder file lives in the drive root, so tests that exercise the real
    /// resolver/registry require this. On Windows the system-drive root is typically not writable
    /// without elevation, in which case those tests are skipped.
    /// </summary>
    public static bool IsDriveRootWritable(string anyPathOnDrive)
    {
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(anyPathOnDrive));
            if (string.IsNullOrEmpty(root))
            {
                return false;
            }

            string probe = Path.Combine(root, $".dup-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
