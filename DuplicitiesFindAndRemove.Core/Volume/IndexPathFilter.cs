namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Matches portable file locations against a directory prefix within the same disk.
/// </summary>
internal static class IndexPathFilter
{
    public static bool IsUnderDirectory(string relativePath, string directoryRelativePath)
    {
        if (directoryRelativePath.Length == 0)
        {
            return true;
        }

        if (string.Equals(relativePath, directoryRelativePath, StringComparison.Ordinal))
        {
            return true;
        }

        string prefix = directoryRelativePath.EndsWith('/')
            ? directoryRelativePath
            : directoryRelativePath + "/";

        return relativePath.StartsWith(prefix, StringComparison.Ordinal);
    }
}
