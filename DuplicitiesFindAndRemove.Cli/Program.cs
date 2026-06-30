using DuplicitiesFindAndRemove.Cli.Commands;
using DuplicitiesFindAndRemove.Core;
using DuplicitiesFindAndRemove.Core.Database;
using DuplicitiesFindAndRemove.Core.Hashing;
using DuplicitiesFindAndRemove.Core.Interfaces;
using DuplicitiesFindAndRemove.Core.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DuplicitiesFindAndRemove.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        string dbPath = Path.Combine(AppContext.BaseDirectory, "duplicates.db");

        services.AddApplicationServices(dbPath);

        var provider = services.BuildServiceProvider();

        try
        {
            using var scope = provider.CreateScope();

            var scopedServices = scope.ServiceProvider;

            var context = scopedServices.GetRequiredService<DuplicateDbContext>();

            await context.Database.EnsureCreatedAsync();

            var dispatcher = new CommandDispatcher(scopedServices);
            return await dispatcher.RunAsync(args);
        }
        finally
        {
            var db = provider.GetRequiredService<SqliteInMemoryDatabase>();

            Console.WriteLine("Persisting DB to disk...");
            db.Persist();

            db.Dispose();
        }
    }

    internal static void AddApplicationServices(this ServiceCollection services, string dbPath)
    {
        // SQLite memory DB
        services.AddSingleton(new SqliteInMemoryDatabase(dbPath));

        // EF Core
        services.AddDbContext<DuplicateDbContext>((sp, options) =>
        {
            var memDb = sp.GetRequiredService<SqliteInMemoryDatabase>();
            options.UseSqlite(memDb.MemoryConnection);
        });

        // Core services
        services.AddSingleton<IFileSystemAbstraction, FileSystemAbstraction>();
        services.AddSingleton<IFileContentHasher, Blake3Hasher>();
        services.AddSingleton<IDuplicateVerifier, ByteCompareVerifier>();
        services.AddSingleton(new DuplicateDetectionOptions());
        services.AddScoped<IDuplicateIndex>(sp => sp.GetRequiredService<DuplicateDbContext>());
        services.AddScoped<IDuplicateScanner, DuplicateScanner>();

        // CLI commands
        services.AddScoped<ScanCommand>();
        services.AddScoped<ReportCommand>();
        services.AddScoped<PruneCommand>();
    }
}
