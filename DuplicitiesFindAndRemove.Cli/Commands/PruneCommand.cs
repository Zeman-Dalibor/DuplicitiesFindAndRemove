using Microsoft.EntityFrameworkCore;
using DuplicitiesFindAndRemove.Core.Database;

namespace DuplicitiesFindAndRemove.Cli.Commands;

internal sealed class PruneCommand
{
    private readonly DuplicateDbContext db;

    public PruneCommand(DuplicateDbContext db)
    {
        this.db = db;
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
            .ToDictionaryAsync(x => x.Id, x => x.Path);

        foreach (var duplicate in duplicates)
        {
            if (!File.Exists(duplicate.Path))
            {
                Console.WriteLine($"File does not exist: {duplicate.Path}");
                continue;
            }

            string fileName = Path.GetFileName(duplicate.Path);
            string targetPath = GetUniqueTargetPath(quarantineDirectory, fileName);

            if (!originalsById.TryGetValue(duplicate.DuplicateOfFileId, out string? originalPath))
            {
                await db.SaveChangesAsync();
                await Console.Error.WriteLineAsync($"FATAL: Original file record not found for duplicate: {duplicate.Path}. " +
                                        $"Target path: {targetPath}");
                return ExitCode.Error;
            }

            if (!File.Exists(originalPath))
            {
                await db.SaveChangesAsync();
                await Console.Error.WriteLineAsync($"FATAL: Original file is missing before moving the duplicate: {originalPath}. " + Environment.NewLine +
                                        $"Duplicate path: {duplicate.Path}" + Environment.NewLine +
                                        $"Target path: {targetPath}");
                return ExitCode.Error;
            }

            Console.WriteLine($"Moving: {duplicate.Path} -> {targetPath}");
            File.Move(duplicate.Path, targetPath);
            duplicate.Path = targetPath;

            if (!File.Exists(originalPath))
            {
                await db.SaveChangesAsync();
                await Console.Error.WriteLineAsync($"FATAL: Original file is missing after moving the duplicate: {originalPath}. " + Environment.NewLine +
                                        $"Duplicate path: {duplicate.Path}" + Environment.NewLine +
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
