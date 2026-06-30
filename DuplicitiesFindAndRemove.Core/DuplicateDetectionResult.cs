using System.Collections.Immutable;
using DuplicitiesFindAndRemove.Core.Database;

namespace DuplicitiesFindAndRemove.Core;

public sealed record DuplicateDetectionResult
{
    public IList<FileRecordEntity> ScannedFiles { get; init; } = new List<FileRecordEntity>();

    public IList<FileRecordEntity> ConfirmedDuplicates { get; init; } = new List<FileRecordEntity>();

    public IList<FileRecordEntity> Candidates { get; init; } = new List<FileRecordEntity>();

    public int NewOrUpdatedFilesCount { get; init; }

    public int SkippedFilesCount { get; init; }

    public int ConfirmedDuplicatesCount => ConfirmedDuplicates.Count;
}
