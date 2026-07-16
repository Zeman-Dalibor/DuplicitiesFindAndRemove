namespace DuplicitiesFindAndRemove.Core.FileSystemHelpers;

/// <summary>
/// File metadata obtained from a single stat (one <see cref="System.IO.FileInfo"/> access):
/// the size in bytes and the last-write time expressed in nanoseconds. Reading both values
/// together avoids a second metadata round-trip to the disk.
/// </summary>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="LastWriteTimeUtcNanoseconds">
/// Last-write time in UTC expressed as nanoseconds, or <c>null</c> when unavailable.
/// </param>
public readonly record struct FileMetadata(long SizeBytes, long? LastWriteTimeUtcNanoseconds);
