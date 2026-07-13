namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Normalizes absolute file paths into volume-relative paths using forward slashes.
/// </summary>
internal static class RelativePathNormalizer
{
    public static string ToRelativePath(string fullPath, string volumeRootPath)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(volumeRootPath);

        string normalizedFullPath = Path.GetFullPath(fullPath);
        string normalizedVolumeRoot = NormalizeVolumeRoot(volumeRootPath);

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!normalizedFullPath.StartsWith(normalizedVolumeRoot, comparison))
        {
            throw new ArgumentException(
                $"File path '{fullPath}' is not under volume root '{volumeRootPath}'.",
                nameof(fullPath));
        }

        string relativePath = Path.GetRelativePath(normalizedVolumeRoot, normalizedFullPath);
        return NormalizeSeparators(relativePath);
    }

    public static string NormalizeSeparators(string path)
        => path.Replace('\\', '/');

    private static string NormalizeVolumeRoot(string volumeRootPath)
    {
        string root = Path.GetFullPath(volumeRootPath);

        if (!root.EndsWith(Path.DirectorySeparatorChar) && !root.EndsWith(Path.AltDirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        return root;
    }
}
