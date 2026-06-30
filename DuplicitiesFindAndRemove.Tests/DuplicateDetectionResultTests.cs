using DuplicitiesFindAndRemove.Core;
using DuplicitiesFindAndRemove.Core.Database;
using Xunit;

namespace DuplicitiesFindAndRemove.Tests;

public class DuplicateDetectionResultTests
{
    [Fact]
    public void Collections_AreEmpty_ByDefault()
    {
        var result = new DuplicateDetectionResult();

        Assert.Empty(result.ScannedFiles);
        Assert.Empty(result.ConfirmedDuplicates);
        Assert.Empty(result.Candidates);
        Assert.Equal(0, result.ConfirmedDuplicatesCount);
    }

    [Fact]
    public void ConfirmedDuplicatesCount_ReflectsConfirmedDuplicates()
    {
        var result = new DuplicateDetectionResult();

        result.ConfirmedDuplicates.Add(new FileRecordEntity());
        result.ConfirmedDuplicates.Add(new FileRecordEntity());

        Assert.Equal(2, result.ConfirmedDuplicatesCount);
    }
}
