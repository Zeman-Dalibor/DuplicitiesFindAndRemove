namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IIndexChecker
{
    Task<IndexCheckResult> CheckAsync(
        string filterDirectory,
        CancellationToken cancellationToken = default);
}
