using DuplicitiesFindAndRemove.Core.Volume;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class IndexPathFilterTests
{
    [Theory]
    [InlineData("foo/bar/file.txt", "foo/bar", true)]
    [InlineData("foo/bar", "foo/bar", true)]
    [InlineData("foo/bar2/file.txt", "foo/bar", false)]
    [InlineData("foo/file.txt", "foo/bar", false)]
    [InlineData("anything.txt", "", true)]
    public void IsUnderDirectory_MatchesExpectedPaths(string relativePath, string directoryRelativePath, bool expected)
    {
        Assert.Equal(expected, IndexPathFilter.IsUnderDirectory(relativePath, directoryRelativePath));
    }
}
