using DuplicitiesFindAndRemove.Cli;
using DuplicitiesFindAndRemove.Core.Database;
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

        args = new[] { "report" };
        await dispatcher.RunAsync(args);

        File.Move(Path.Combine(pathToSimpleSmallFiles, "a.txt"), Path.Combine(deleteFolder, "a_moved.txt"));

        args = new[] { "prune", deleteFolder };
        await dispatcher.RunAsync(args);

        string output = sw.ToString();
        string errorOutput = swe.ToString();
        testOutputHelper.WriteLine(output);

        testOutputHelper.WriteLine("Errors:");
        testOutputHelper.WriteLine(errorOutput);

        Assert.Contains("Moving:", output);
        Assert.Contains("FATAL: Original file is missing after mov", errorOutput);
        Assert.True(File.Exists(Path.Combine(deleteFolder, "b.txt")));
        Assert.False(File.Exists(Path.Combine(deleteFolder, "d.txt")));
    }
}
