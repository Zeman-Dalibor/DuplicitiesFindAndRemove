using DuplicitiesFindAndRemove.Core;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class ArgumentExceptionExTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ThrowIfNullOrWhiteSpace_Throws_ForInvalidValue(string? value)
    {
        Assert.Throws<ArgumentException>(() => ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(value!));
    }

    [Fact]
    public void ThrowIfNullOrWhiteSpace_DoesNotThrow_ForValidValue()
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace("valid");
    }
}
