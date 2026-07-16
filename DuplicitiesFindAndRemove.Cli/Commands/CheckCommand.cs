using DuplicitiesFindAndRemove.Core.Interfaces;

namespace DuplicitiesFindAndRemove.Cli.Commands;

internal sealed class CheckCommand
{
    private readonly IIndexChecker indexChecker;

    public CheckCommand(IIndexChecker indexChecker)
    {
        this.indexChecker = indexChecker;
    }

    public async Task<ExitCode> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Missing directory path.");
            return ExitCode.Error;
        }

        string rootPath = args[1];

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            Console.WriteLine("Directory path is empty.");
            return ExitCode.Error;
        }

        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine($"Directory does not exist: {rootPath}");
            return ExitCode.Error;
        }

        Console.WriteLine($"Checking index for: {rootPath}");

        var result = await indexChecker.CheckAsync(rootPath);

        Console.WriteLine($"Deleted canonical records: {result.DeletedCanonicalRecordsCount}");
        Console.WriteLine($"Deleted duplicate records: {result.DeletedDuplicateRecordsCount}");
        Console.WriteLine($"Cascade-deleted duplicates: {result.CascadeDeletedDuplicatesCount}");
        Console.WriteLine($"Updated records: {result.UpdatedRecordsCount}");
        Console.WriteLine($"Unchanged records: {result.UnchangedRecordsCount}");
        Console.WriteLine($"Unmounted records skipped: {result.UnmountedRecordsCount}");
        Console.WriteLine($"Errors: {result.ErrorRecordsCount}");
        Console.WriteLine();

        return ExitCode.Success;
    }
}
