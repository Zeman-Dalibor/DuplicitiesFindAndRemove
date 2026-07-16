namespace DuplicitiesFindAndRemove.Core;

public sealed record IndexCheckResult
{
    public int DeletedCanonicalRecordsCount { get; set; }

    public int DeletedDuplicateRecordsCount { get; set; }

    public int CascadeDeletedDuplicatesCount { get; set; }

    public int UpdatedRecordsCount { get; set; }

    public int UnchangedRecordsCount { get; set; }

    public int UnmountedRecordsCount { get; set; }

    public int ErrorRecordsCount { get; set; }
}
