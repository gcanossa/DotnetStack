using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GKit.Tests.Infrastructure;

/// <summary>
/// Owns a shared in-memory SQLite connection so that several <see cref="DbContext"/> instances
/// can see the same database. Using real SQLite (rather than the EF in-memory provider) matters
/// here: the interceptors under test issue relational queries and rely on query filters.
/// </summary>
public sealed class SqliteFixture : IDisposable
{
  private readonly SqliteConnection _connection;

  public SqliteFixture()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    _connection.Open();
  }

  public DbContextOptions<T> OptionsFor<T>(Action<DbContextOptionsBuilder>? configure = null)
    where T : DbContext
  {
    var builder = new DbContextOptionsBuilder<T>().UseSqlite(_connection);
    configure?.Invoke(builder);
    return builder.Options;
  }

  public void Dispose() => _connection.Dispose();
}
