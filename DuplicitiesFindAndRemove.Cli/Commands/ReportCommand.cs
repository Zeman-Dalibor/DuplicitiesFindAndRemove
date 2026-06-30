using DuplicitiesFindAndRemove.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace DuplicitiesFindAndRemove.Cli.Commands;

internal sealed class ReportCommand
{
    private readonly DuplicateDbContext db;

    public ReportCommand(DuplicateDbContext db)
    {
        this.db = db;
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

        foreach (var duplicate in duplicates)
        {
            Console.WriteLine($"{duplicate.Id}: {duplicate.Path} -> duplicate of {duplicate.DuplicateOfFileId}");
        }

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

        foreach (var group in groupedDuplicates)
        {
            Console.WriteLine($"Duplicate group for file {group.First().Original.Path} (ID: {group.Key}) - {group.Count()} duplicates:");
            foreach (var duplicate in group)
            {
                Console.WriteLine($"  {duplicate.Duplicate.Id}: {duplicate.Duplicate.Path}");
            }
        }

        return ExitCode.Success;
    }
}
