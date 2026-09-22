using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GKit.Test1.Infrastructure;

/// <summary>
/// Owns a shared in-memory SQLite connection so several <see cref="DbContext"/> instances see the
/// same database. Real SQLite rather than the EF in-memory provider: query filters and relational
/// translation are exactly what the GKit interceptors rely on, and the in-memory provider has
/// neither.
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
