using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DuplicitiesFindAndRemove.Cli.Commands;

internal sealed class ReportCommand
{
    private readonly DuplicateDbContext db;
    private readonly IDiskRegistry diskRegistry;

    public ReportCommand(DuplicateDbContext db, IDiskRegistry diskRegistry)
    {
        this.db = db;
        this.diskRegistry = diskRegistry;
    }

    public async Task<ExitCode> ExecuteAsync(string[] args)
    {
        var duplicates = await db.Duplicates
            .ToListAsync();

        if (duplicates.Count == 0)
        {
            Console.WriteLine("No duplicates found.");
            return ExitCode.NoDuplicatesFound;
        }

        Console.WriteLine($"Found {duplicates.Count} duplicates:");
        foreach (var duplicate in duplicates)
        {
            Console.WriteLine($"{Describe(duplicate)} (id:{duplicate.Id}) -> duplicate of {duplicate.DuplicateOfFileId}");
        }
        Console.WriteLine();

        // The join is translated to SQL, but the grouping is done in memory because EF Core
        // cannot translate a GroupBy that materializes whole entities into the result.
        var joinedDuplicates = await db.Duplicates
            .Join(
                db.FileRecords,
                dup => dup.DuplicateOfFileId,
                original => original.Id,
                (dup, original) => new { Duplicate = dup, Original = original }
            )
            .ToListAsync();

        var groupedDuplicates = joinedDuplicates.GroupBy(arg => arg.Duplicate.DuplicateOfFileId);

        Console.WriteLine($"Duplicate groups:");
        foreach (var group in groupedDuplicates)
        {
            Console.WriteLine($"Duplicate group for file {Describe(group.First().Original)} (ID: {group.Key}) - {group.Count()} duplicates:");
            foreach (var duplicate in group)
            {
                Console.WriteLine($"  {Describe(duplicate.Duplicate)}");
            }
        }
        Console.WriteLine();

        return ExitCode.Success;
    }

    // Prefers the reconstructed absolute path; falls back to the portable location when the file's
    // disk is not currently mounted.
    private string Describe(IFileRecord record)
        => diskRegistry.TryGetAbsolutePath(record.Location) ?? record.Location.ToString();
}
