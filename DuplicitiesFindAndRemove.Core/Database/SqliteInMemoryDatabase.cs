using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DuplicitiesFindAndRemove.Core.Database;

public class SqliteInMemoryDatabase : IDisposable
{
    public SqliteConnection MemoryConnection { get; }

    private readonly string filePath;

    public SqliteInMemoryDatabase(string filePath)
    {
        this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        MemoryConnection = new SqliteConnection("Data Source=:memory:");
        MemoryConnection.Open();

        using var fileConn = new SqliteConnection($"Data Source={this.filePath}");
        fileConn.Open();

        // Load file DB -> memory DB
        fileConn.BackupDatabase(MemoryConnection);
    }

    public DbContextOptions<DuplicateDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<DuplicateDbContext>()
            .UseSqlite(MemoryConnection)
            .Options;
    }

    public void Persist()
    {
        using var fileConn = new SqliteConnection($"Data Source={filePath}");
        fileConn.Open();

        // Save memory DB -> file DB
        MemoryConnection.BackupDatabase(fileConn);
    }

    public void Dispose()
    {
        Persist();
        MemoryConnection.Dispose();
        GC.SuppressFinalize(this);
    }
}