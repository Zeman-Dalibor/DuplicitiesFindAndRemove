namespace DuplicitiesFindAndRemove.Cli.Commands;

using DuplicitiesFindAndRemove.Core;
using System;
using System.Threading.Tasks;

internal sealed class ScanCommand
{
    private readonly IDuplicateScanner scanner;

    public ScanCommand(IDuplicateScanner scanner)
    {
        this.scanner = scanner;
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

        Console.WriteLine($"Scanning: {rootPath}");

        var result = await scanner.ScanAsync(rootPath);

        Console.WriteLine($"Processed: {result.ScannedFiles.Count}");
        Console.WriteLine($"New or updated: {result.NewOrUpdatedFilesCount}");
        Console.WriteLine($"Candidates: {result.Candidates.Count}");
        Console.WriteLine($"Confirmed duplicates: {result.ConfirmedDuplicatesCount}");

        return ExitCode.Success;
    }
}
