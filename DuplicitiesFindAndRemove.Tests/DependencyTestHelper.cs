using DuplicitiesFindAndRemove.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace DuplicitiesFindAndRemove.Tests;

public static class DependencyTestHelper
{
    public static IServiceProvider GetServiceProvider(string dbFileName = "test-duplicates.db")
    {
        var services = new ServiceCollection();

        string dbPath = Path.Combine(AppContext.BaseDirectory, dbFileName);
        services.AddApplicationServices(dbPath);

        return services.BuildServiceProvider();
    }
}
