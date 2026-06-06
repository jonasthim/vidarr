using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vidarr.Catalog;

namespace Vidarr.Catalog.Tests;

internal static class InMemoryDb
{
    public static (VidarrDbContext Db, SqliteConnection Conn) Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<VidarrDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new VidarrDbContext(opts);
        db.Database.EnsureCreated();
        return (db, conn);
    }
}
