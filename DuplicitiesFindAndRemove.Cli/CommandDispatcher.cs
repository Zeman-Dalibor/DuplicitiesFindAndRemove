using Microsoft.Extensions.DependencyInjection;

namespace DuplicitiesFindAndRemove.Cli;

internal sealed class CommandDispatcher
{
    private readonly IServiceProvider services;

    public CommandDispatcher(IServiceProvider services)
    {
        this.services = services;
    }

    public async Task<ExitCode> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return ExitCode.Error;
        }

        string commandName = args[0].ToLowerInvariant();

        return commandName switch
        {
            "scan" => await RunScanAsync(args),
            "check" => await RunCheckAsync(args),
            "report" => await RunReportAsync(args),
            "prune" => await RunPruneAsync(args),
            "help" or "--help" or "-h" => RunHelp(),
            _ => UnknownCommand(commandName)
        };
    }

    private async Task<ExitCode> RunScanAsync(string[] args)
    {
        var command = services.GetRequiredService<Commands.ScanCommand>();
        return await command.ExecuteAsync(args);
    }

    private async Task<ExitCode> RunCheckAsync(string[] args)
    {
        var command = services.GetRequiredService<Commands.CheckCommand>();
        return await command.ExecuteAsync(args);
    }

    private async Task<ExitCode> RunReportAsync(string[] args)
    {
        var command = services.GetRequiredService<Commands.ReportCommand>();
        return await command.ExecuteAsync(args);
    }

    private async Task<ExitCode> RunPruneAsync(string[] args)
    {
        var command = services.GetRequiredService<Commands.PruneCommand>();
        return await command.ExecuteAsync(args);
    }

    private static ExitCode RunHelp()
    {
        PrintHelp();
        return ExitCode.Success;
    }

    private static ExitCode UnknownCommand(string commandName)
    {
        Console.WriteLine($"Unknown command: {commandName}");
        PrintHelp();
        return ExitCode.Error;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
@"Usage:
  DuplicateFinder.ConsoleApp scan <directory_path>
  DuplicateFinder.ConsoleApp check <directory_path>
  DuplicateFinder.ConsoleApp report
  DuplicateFinder.ConsoleApp prune

Commands:
  scan    Scans the directory and updates the index
  check   Validates indexed files under the directory and repairs the index
  report  Displays the found duplicates
  prune   Performs deletion or moves to quarantine");
    }
}
