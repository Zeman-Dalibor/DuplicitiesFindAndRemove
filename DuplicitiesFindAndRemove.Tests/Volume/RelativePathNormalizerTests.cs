using DuplicitiesFindAndRemove.Core.Volume;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class RelativePathNormalizerTests
{
    [Fact]
    public void ToRelativePath_UsesForwardSlashes()
    {
        string relative = RelativePathNormalizer.ToRelativePath(
            @"C:\Photos\2024\image.jpg",
            @"C:\");

        Assert.Equal("Photos/2024/image.jpg", relative);
    }

    [Fact]
    public void ToRelativePath_IsCaseInsensitive_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string relative = RelativePathNormalizer.ToRelativePath(
            @"C:\Photos\Image.JPG",
            @"c:\");

        Assert.Equal("Photos/Image.JPG", relative);
    }

    [Fact]
    public void ToRelativePath_Throws_WhenFileIsOutsideVolumeRoot()
    {
        Assert.Throws<ArgumentException>(() =>
            RelativePathNormalizer.ToRelativePath(@"C:\Photos\a.jpg", @"D:\"));
    }

    [Theory]
    [InlineData(@"a\b\c.txt", "a/b/c.txt")]
    [InlineData(@"a/b/c.txt", "a/b/c.txt")]
    public void NormalizeSeparators_ReplacesBackslashes(string input, string expected)
    {
        Assert.Equal(expected, RelativePathNormalizer.NormalizeSeparators(input));
    }
}
