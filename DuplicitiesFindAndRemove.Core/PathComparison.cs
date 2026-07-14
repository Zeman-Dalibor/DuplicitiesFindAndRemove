namespace DuplicitiesFindAndRemove.Core;

/// <summary>
/// Operating-system-aware comparison of file system paths. Windows path names are
/// case-insensitive, whereas Linux file systems are case-sensitive, so "A.txt" and
/// "a.txt" refer to two different physical files. Using a single, platform-correct
/// comparison prevents distinct files from being mistaken for the same one on Linux.
/// </summary>
public static class PathComparison
{
    /// <summary>
    /// The <see cref="StringComparison"/> that matches the current platform's path
    /// case sensitivity.
    /// </summary>
    public static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Returns <c>true</c> when both paths reference the same location, taking the
    /// platform's case sensitivity into account. Both paths are normalized with
    /// <see cref="Path.GetFullPath(string)"/> before being compared.
    /// </summary>
    public static bool AreSamePath(string firstPath, string secondPath)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(firstPath);
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(secondPath);

        return string.Equals(
            Path.GetFullPath(firstPath),
            Path.GetFullPath(secondPath),
            Comparison);
    }
}
