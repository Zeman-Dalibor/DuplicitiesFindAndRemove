using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Core.Volume;

/// <summary>
/// Reads (or creates) the placeholder identity file in a disk root and exposes the resulting
/// <see cref="DiskIdentity"/>. The identity for a given root is computed only once and cached,
/// because a folder can never belong to more than one disk at a time.
/// </summary>
public sealed class DiskIdentityProvider : IDiskIdentityProvider
{
    /// <summary>
    /// Name of the placeholder file. It lives in the disk root and travels with the disk, which
    /// makes it a fully portable disk identifier.
    /// </summary>
    public const string PlaceholderFileName = ".duplicities-disk-id.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object sync = new();
    private readonly Dictionary<string, DiskIdentity> cache = new(StringComparer.OrdinalIgnoreCase);

    public DiskIdentity GetOrCreate(string diskRootPath)
    {
        ArgumentExceptionEx.ThrowIfNullOrWhiteSpace(diskRootPath);

        string root = Path.GetFullPath(diskRootPath);

        lock (sync)
        {
            if (cache.TryGetValue(root, out DiskIdentity? cached))
            {
                return cached;
            }

            DiskIdentity identity = ReadOrCreate(root);
            cache[root] = identity;
            return identity;
        }
    }

    private static DiskIdentity ReadOrCreate(string diskRootPath)
    {
        string filePath = Path.Combine(diskRootPath, PlaceholderFileName);

        if (File.Exists(filePath))
        {
            DiskIdentity? existing = TryRead(filePath);
            if (existing is not null)
            {
                return existing;
            }

            // The file exists but is unreadable or corrupt. Rewrite it with a fresh identity so
            // the disk remains identifiable rather than failing every scan.
        }

        var created = new DiskIdentity(Guid.NewGuid(), TryGetVolumeLabel(diskRootPath));
        Write(filePath, created);
        return created;
    }

    private static DiskIdentity? TryRead(string filePath)
    {
        try
        {
            using FileStream stream = File.OpenRead(filePath);
            PlaceholderFileModel? model = JsonSerializer.Deserialize<PlaceholderFileModel>(stream, SerializerOptions);

            if (model is null || !Guid.TryParse(model.Id, out Guid id) || id == Guid.Empty)
            {
                return null;
            }

            return new DiskIdentity(id, model.Label);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Write(string filePath, DiskIdentity identity)
    {
        var model = new PlaceholderFileModel
        {
            Id = identity.Id.ToString("D"),
            Label = identity.Label,
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            Application = "DuplicitiesFindAndRemove"
        };

        using FileStream stream = File.Create(filePath);
        JsonSerializer.Serialize(stream, model, SerializerOptions);
    }

    private static string? TryGetVolumeLabel(string diskRootPath)
    {
        try
        {
            var drive = new DriveInfo(diskRootPath);
            return drive.IsReady && !string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.VolumeLabel : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private sealed class PlaceholderFileModel
    {
        public string Id { get; set; } = string.Empty;

        public string? Label { get; set; }

        public string? CreatedUtc { get; set; }

        public string? Application { get; set; }
    }
}
