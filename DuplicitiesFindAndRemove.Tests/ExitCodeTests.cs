using DuplicitiesFindAndRemove.Cli;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class ExitCodeTests
{
    [Fact]
    public void KnownExitCodes_HaveExpectedValues()
    {
        Assert.Equal(0, ExitCode.Success.Value);
        Assert.Equal(1, ExitCode.NoDuplicatesFound.Value);
        Assert.Equal(-1, ExitCode.Error.Value);
    }

    [Fact]
    public void ImplicitConversion_ReturnsUnderlyingValue()
    {
        int value = ExitCode.NoDuplicatesFound;

        Assert.Equal(1, value);
    }
}
