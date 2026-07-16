using Microsoft.EntityFrameworkCore;
using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Cli.Commands;

internal sealed class PruneCommand
{
    private readonly DuplicateDbContext db;
    private readonly IDiskRegistry diskRegistry;
    private readonly IVolumePathResolver volumePathResolver;

    public PruneCommand(DuplicateDbContext db, IDiskRegistry diskRegistry, IVolumePathResolver volumePathResolver)
    {
        this.db = db;
        this.diskRegistry = diskRegistry;
        this.volumePathResolver = volumePathResolver;
    }

    public async Task<ExitCode> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Missing quarantine directory path.");
            return ExitCode.Error;
        }

        string quarantineDirectory = args[1];

        if (string.IsNullOrWhiteSpace(quarantineDirectory))
        {
            Console.WriteLine("Quarantine directory path is empty.");
            return ExitCode.Error;
        }

        Directory.CreateDirectory(quarantineDirectory);

        // Get all duplicates from the separate duplicates table
        var duplicates = await db.Duplicates
            .ToListAsync();

        if (duplicates.Count == 0)
        {
            Console.WriteLine("No duplicates found.");
            return ExitCode.NoDuplicatesFound;
        }

        // Get all original (canonical) files and create a dictionary for quick lookup
        var originalsById = await db.FileRecords
            .ToDictionaryAsync(x => x.Id, x => new FileLocation(x.DiskId, x.RelativePath));

        foreach (var duplicate in duplicates)
        {
            string? duplicatePath = diskRegistry.TryGetAbsolutePath(duplicate.Location);
            if (duplicatePath is null || !File.Exists(duplicatePath))
            {
                Console.WriteLine($"File does not exist: {duplicate.Location}");
                continue;
            }

            string fileName = Path.GetFileName(duplicatePath);
            string targetPath = GetUniqueTargetPath(quarantineDirectory, fileName);

            if (!originalsById.TryGetValue(duplicate.DuplicateOfFileId, out FileLocation? originalLocation))
            {
                await db.SaveChangesAsync();
                await Console.Error.WriteLineAsync($"FATAL: Original file record not found for duplicate: {duplicatePath}. " +
                                        $"Target path: {targetPath}");
                return ExitCode.Error;
            }

            string? originalPath = diskRegistry.TryGetAbsolutePath(originalLocation);
            if (originalPath is null || !File.Exists(originalPath))
            {
                await db.SaveChangesAsync();
                await Console.Error.WriteLineAsync($"FATAL: Original file is missing before moving the duplicate: {originalLocation}. " + Environment.NewLine +
                                        $"Duplicate path: {duplicatePath}" + Environment.NewLine +
                                        $"Target path: {targetPath}");
                return ExitCode.Error;
            }

            Console.WriteLine($"Moving: {duplicatePath} -> {targetPath}");
            File.Move(duplicatePath, targetPath);

            // The duplicate now lives at its quarantine location, so update its portable location.
            FileLocation movedLocation = volumePathResolver.Resolve(targetPath);
            duplicate.DiskId = movedLocation.DiskId;
            duplicate.RelativePath = movedLocation.RelativePath;

            if (!File.Exists(originalPath))
            {
                await db.SaveChangesAsync();
                await Console.Error.WriteLineAsync($"FATAL: Original file is missing after moving the duplicate: {originalPath}. " + Environment.NewLine +
                                        $"Duplicate path: {targetPath}" + Environment.NewLine +
                                        $"Target path: {targetPath}");
                return ExitCode.Error;
            }

            // Update the duplicate record in the database
            await db.SaveChangesAsync();
        }

        await db.SaveChangesAsync();
        return ExitCode.Success;
    }

    private static string GetUniqueTargetPath(string directory, string fileName)
    {
        string targetPath = Path.Combine(directory, fileName);

        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        for (int i = 1; ; i++)
        {
            targetPath = Path.Combine(directory, $"{name} ({i}){extension}");

            if (!File.Exists(targetPath))
            {
                return targetPath;
            }
        }
    }
}
