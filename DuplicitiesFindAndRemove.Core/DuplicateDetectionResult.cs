using System.Collections.Immutable;
using DuplicitiesFindAndRemove.Core.Database;

namespace DuplicitiesFindAndRemove.Core;

public sealed record DuplicateDetectionResult
{
    public int NewOrUpdatedFilesCount { get; set; } = 0;

    public int SkippedFilesCount { get; set; } = 0;

    public int ConfirmedDuplicatesCount { get; set; } = 0;

    public int ErrorFilesCount { get; set; } = 0;
}
