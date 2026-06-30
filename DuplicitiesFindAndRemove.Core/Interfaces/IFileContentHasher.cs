namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IFileContentHasher
{
    Task<byte[]> ComputeFullHashAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<byte[]> ComputeSampleHashAsync(
        string path,
        CancellationToken cancellationToken = default);
}
