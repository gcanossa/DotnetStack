using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GKit.Tests.Infrastructure;

/// <summary>
/// Records the SQL EF actually executes, so a test can assert that a code path issues no query
/// rather than merely producing the right answer.
/// <para>
/// Counting <c>ExecuteReader</c> calls is not enough: on SQLite an <c>INSERT ... RETURNING</c>
/// also goes through a reader, so the save batch itself would be counted. Classify by the
/// command text instead.
/// </para>
/// </summary>
public sealed class CommandCounter : DbCommandInterceptor
{
  private readonly ConcurrentQueue<string> _commands = new();

  public IReadOnlyCollection<string> Commands => [.. _commands];

  /// <summary>Commands that read data — as opposed to the INSERT/UPDATE/DELETE of a save.</summary>
  public int Selects => _commands.Count(c =>
    c.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

  public void Reset() => _commands.Clear();

  private void Record(DbCommand command) => _commands.Enqueue(command.CommandText);

  public override InterceptionResult<DbDataReader> ReaderExecuting(
    DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
  {
    Record(command);
    return result;
  }

  public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
    DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
    CancellationToken cancellationToken = default)
  {
    Record(command);
    return ValueTask.FromResult(result);
  }

  public override InterceptionResult<int> NonQueryExecuting(
    DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
  {
    Record(command);
    return result;
  }

  public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
    DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
  {
    Record(command);
    return ValueTask.FromResult(result);
  }

  public override InterceptionResult<object> ScalarExecuting(
    DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
  {
    Record(command);
    return result;
  }

  public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
    DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
    CancellationToken cancellationToken = default)
  {
    Record(command);
    return ValueTask.FromResult(result);
  }
}
