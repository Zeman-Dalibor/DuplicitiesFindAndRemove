using DuplicitiesFindAndRemove.Core.Database;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class FileRecordEntityTests
{
    [Fact]
    public void HasSampleHash_IsTrue_WhenSampleHashHasBytes()
    {
        var record = new FileRecordEntity { SampleHash = new byte[] { 1, 2, 3 } };

        Assert.True(record.HasSampleHash);
    }

    [Fact]
    public void HasSampleHash_IsFalse_WhenSampleHashIsNullOrEmpty()
    {
        Assert.False(new FileRecordEntity { SampleHash = null }.HasSampleHash);
        Assert.False(new FileRecordEntity { SampleHash = Array.Empty<byte>() }.HasSampleHash);
    }

    [Fact]
    public void HasFullHash_IsTrue_WhenFullHashHasBytes()
    {
        var record = new FileRecordEntity { FullHash = new byte[] { 9 } };

        Assert.True(record.HasFullHash);
    }

    [Fact]
    public void HasFullHash_IsFalse_WhenFullHashIsNullOrEmpty()
    {
        Assert.False(new FileRecordEntity { FullHash = null }.HasFullHash);
        Assert.False(new FileRecordEntity { FullHash = Array.Empty<byte>() }.HasFullHash);
    }

    [Fact]
    public void IsDuplicate_ReflectsDuplicateOfFileId()
    {
        Assert.True(new FileRecordEntity { DuplicateOfFileId = 5 }.IsDuplicate);
        Assert.False(new FileRecordEntity { DuplicateOfFileId = null }.IsDuplicate);
    }

    [Fact]
    public void State_DefaultsToNotScanned()
    {
        Assert.Equal(ScanState.NotScanned, new FileRecordEntity().State);
    }
}
