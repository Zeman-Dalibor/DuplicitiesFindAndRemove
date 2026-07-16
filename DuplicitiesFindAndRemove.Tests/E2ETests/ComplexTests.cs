using DuplicitiesFindAndRemove.Cli;
using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace DuplicitiesFindAndRemove.Tests.E2ETests;

public class ComplexTests
{
    private readonly ITestOutputHelper testOutputHelper;

    public ComplexTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task DispatcherScanTests()
    {
        if (!TestEnvironment.IsDriveRootWritable(Environment.CurrentDirectory))
        {
            return; // The placeholder identity file requires write access to the drive root.
        }

        string pathToSimpleSmallFiles = Path.Combine(Environment.CurrentDirectory, "TestData", "SimpleSmallFiles");

        var sw = new StringWriter();
        Console.SetOut(sw);
        var serviceProvider = DependencyTestHelper.GetServiceProvider("SmallFiles.db");

        string[] args = { "scan", pathToSimpleSmallFiles };
        var dispatcher = new CommandDispatcher(serviceProvider);
        await dispatcher.RunAsync(args);

        var dbContext = serviceProvider.GetService<DuplicateDbContext>();
        Assert.NotNull(dbContext);

        var duplicate = await dbContext.Duplicates.FirstOrDefaultAsync();
        Assert.NotNull(duplicate);

        args = new[] { "report" };
        await dispatcher.RunAsync(args);

        string output = sw.ToString();
        testOutputHelper.WriteLine(output);
    }

    [Fact]
    public async Task PruneTests()
    {
        if (!TestEnvironment.IsDriveRootWritable(Environment.CurrentDirectory))
        {
            return; // The placeholder identity file requires write access to the drive root.
        }

        string pathToSimpleSmallFiles = Path.Combine(Environment.CurrentDirectory, "TestData", "PruneSmallFiles");
        string deleteFolder = Path.Combine(Environment.CurrentDirectory, "ToDelete");
        if (Directory.Exists(deleteFolder))
        {
            Directory.Delete(deleteFolder, recursive: true);
        }

        var sw = new StringWriter();
        Console.SetOut(sw);
        var swe = new StringWriter();
        Console.SetError(swe);
        var serviceProvider = DependencyTestHelper.GetServiceProvider("PruneSmallFiles.db");

        string[] args = { "scan", pathToSimpleSmallFiles };
        var dispatcher = new CommandDispatcher(serviceProvider);
        await dispatcher.RunAsync(args);

        var dbContext = serviceProvider.GetService<DuplicateDbContext>();
        Assert.NotNull(dbContext);

        var duplicate = await dbContext.Duplicates.FirstOrDefaultAsync();
        Assert.NotNull(duplicate);

        args = new[] { "prune", deleteFolder };
        await dispatcher.RunAsync(args);

        string output = sw.ToString();
        string errorOutput = swe.ToString();
        testOutputHelper.WriteLine(output);

        testOutputHelper.WriteLine("Errors:");
        testOutputHelper.WriteLine(errorOutput);

        Assert.Contains("Moving:", output);
        Assert.Empty(errorOutput);
    }

    [Fact]
    public async Task PruneCanonicalMovedErrorTests()
    {
        if (!TestEnvironment.IsDriveRootWritable(Environment.CurrentDirectory))
        {
            return; // The placeholder identity file requires write access to the drive root.
        }

        string pathToSimpleSmallFiles = Path.Combine(Environment.CurrentDirectory, "TestData", "PruneSmallFilesErrors");
        string deleteFolder = Path.Combine(Environment.CurrentDirectory, "ToDelete");
        if (Directory.Exists(deleteFolder))
        {
            Directory.Delete(deleteFolder, recursive: true);
        }
        Directory.CreateDirectory(deleteFolder);

        var sw = new StringWriter();
        Console.SetOut(sw);
        var swe = new StringWriter();
        Console.SetError(swe);
        var serviceProvider = DependencyTestHelper.GetServiceProvider("PruneSmallFilesErrors.db");

        string[] args = { "scan", pathToSimpleSmallFiles };
        var dispatcher = new CommandDispatcher(serviceProvider);
        await dispatcher.RunAsync(args);

        var dbContext = serviceProvider.GetService<DuplicateDbContext>();
        Assert.NotNull(dbContext);

        var duplicate = await dbContext.Duplicates.FirstOrDefaultAsync();
        Assert.NotNull(duplicate);

        var canonical = await dbContext.FileRecords.FirstOrDefaultAsync();
        Assert.NotNull(canonical);

        args = new[] { "report" };
        await dispatcher.RunAsync(args);

        var diskRegistry = serviceProvider.GetRequiredService<IDiskRegistry>();

        // Move of canonical - this will cause an error during prune, as the canonical file is missing
        string? canonicalPath = diskRegistry.TryGetAbsolutePath(canonical!.Location);
        Assert.NotNull(canonicalPath);
        File.Move(canonicalPath!, Path.Combine(deleteFolder, "canonical_moved.txt"));

        args = new[] { "prune", deleteFolder };
        await dispatcher.RunAsync(args);

        string output = sw.ToString();
        string errorOutput = swe.ToString();
        testOutputHelper.WriteLine(output);

        testOutputHelper.WriteLine("Errors:");
        testOutputHelper.WriteLine(errorOutput);

        Assert.Contains("FATAL: Original file is missing before moving", errorOutput);

        var detectedDuplicates = await dbContext.Duplicates.ToListAsync();

        int movedCount = 0;
        int skippedCount = 0;
        foreach (var dup in detectedDuplicates)
        {
            string? dupPath = diskRegistry.TryGetAbsolutePath(dup.Location);
            string? filename = dupPath is null ? null : Path.GetFileName(dupPath);
            if (filename is not null && File.Exists(Path.Combine(deleteFolder, filename)))
            {
                movedCount++;
            }
            else
            {
                skippedCount++;
            }
        }

        Assert.Equal(0, movedCount);
        Assert.Equal(3, skippedCount);
    }
}
