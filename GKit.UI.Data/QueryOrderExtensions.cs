using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace GKit.UI.Data;

/// <summary>
/// Applies <see cref="GridSort"/> instructions to a query.
/// </summary>
/// <remarks>
/// This replaces the component-library sort extensions the grid code used to depend on. The
/// chaining semantics are deliberately identical: the first sort becomes OrderBy/OrderByDescending
/// and each subsequent one ThenBy/ThenByDescending, with the source returned unchanged when there
/// is nothing to sort by.
/// </remarks>
public static class QueryOrderExtensions
{
  private static readonly MethodInfo _orderByMethod = typeof(Queryable).GetMethods()
    .Where(m => m is { Name: nameof(Queryable.OrderBy), IsGenericMethodDefinition: true })
    .Single(m => m.GetParameters().Length == 2);

  private static readonly MethodInfo _orderByDescendingMethod = typeof(Queryable).GetMethods()
    .Where(m => m is { Name: nameof(Queryable.OrderByDescending), IsGenericMethodDefinition: true })
    .Single(m => m.GetParameters().Length == 2);

  private static readonly MethodInfo _thenByMethod = typeof(Queryable).GetMethods()
    .Where(m => m is { Name: nameof(Queryable.ThenBy), IsGenericMethodDefinition: true })
    .Single(m => m.GetParameters().Length == 2);

  private static readonly MethodInfo _thenByDescendingMethod = typeof(Queryable).GetMethods()
    .Where(m => m is { Name: nameof(Queryable.ThenByDescending), IsGenericMethodDefinition: true })
    .Single(m => m.GetParameters().Length == 2);

  /// <summary>
  /// Orders <paramref name="source"/> by the given sorts, building the key selector with
  /// <paramref name="keySelectorFactory"/>.
  /// </summary>
  [RequiresUnreferencedCode("Builds OrderBy/ThenBy calls over Queryable, which are subject to trimming.")]
  public static IQueryable<T> OrderBy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
    this IQueryable<T> source,
    IEnumerable<GridSort> sorts,
    Func<ParameterExpression, GridSort, Expression> keySelectorFactory)
  {
    IOrderedQueryable<T>? query = null;

    foreach (var sort in sorts)
    {
      var parameter = Expression.Parameter(typeof(T), "x");
      var body = keySelectorFactory(parameter, sort);
      var keySelector = Expression.Lambda(body, parameter);

      if (query is null)
      {
        var orderBy = (sort.Descending ? _orderByDescendingMethod : _orderByMethod)
          .MakeGenericMethod(typeof(T), keySelector.ReturnType);
        query = (IOrderedQueryable<T>?)orderBy.Invoke(null, [source, keySelector]);
      }
      else
      {
        var thenBy = (sort.Descending ? _thenByDescendingMethod : _thenByMethod)
          .MakeGenericMethod(typeof(T), keySelector.ReturnType);
        query = (IOrderedQueryable<T>?)thenBy.Invoke(null, [query, keySelector]);
      }
    }

    return query ?? source;
  }

  /// <summary>
  /// Orders <paramref name="source"/> by dotted property paths, e.g. <c>"Nation.Name"</c>.
  /// </summary>
  [RequiresUnreferencedCode("Resolves properties by name via reflection.")]
  public static IQueryable<T> OrderBy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
    this IQueryable<T> source,
    IEnumerable<GridSort> sorts)
  {
    return OrderBy(source, sorts, (parameter, sort) =>
    {
      var segments = sort.Path.Split('.');
      var property = typeof(T).GetProperty(segments[0])!;
      var result = Expression.Property(parameter, typeof(T), segments[0]);

      for (var i = 1; i < segments.Length; i++)
      {
        result = Expression.Property(result, property!.PropertyType, segments[i]);
        property = property.PropertyType.GetProperty(segments[i])!;
      }

      return result;
    });
  }

  /// <summary>
  /// Orders by dotted property paths, guarding every intermediate step against null.
  /// </summary>
  /// <remarks>
  /// For a path <c>"A.B.C"</c> the key selector becomes
  /// <c>x =&gt; x.A == null ? default : (x.A.B == null ? default : x.A.B.C)</c>, so a null anywhere
  /// along the path sorts as the leaf type's default rather than throwing. Ordering a nullable
  /// navigation without this guard is the common case that breaks server-side sorting.
  /// </remarks>
  [RequiresUnreferencedCode("Resolves properties by name via reflection.")]
  public static IQueryable<T> NullCheckingOrderBy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
    this IQueryable<T> source,
    IEnumerable<GridSort> sorts)
  {
    return OrderBy(source, sorts, (parameter, sort) =>
    {
      var segments = sort.Path.Split('.');
      var property = typeof(T).GetProperty(segments[0])!;
      var members = new List<MemberExpression> { Expression.Property(parameter, typeof(T), segments[0]) };

      for (var i = 1; i < segments.Length; i++)
      {
        members.Add(Expression.Property(members[^1], property!.PropertyType, segments[i]));
        property = property.PropertyType.GetProperty(segments[i])!;
      }

      if (members.Count == 1)
        return members[0];

      // Walk back up the path wrapping the accessor in a null guard per intermediate step, so the
      // outermost member ends up as the outermost condition.
      Expression result = members[^1];
      for (var i = 2; i <= members.Count; i++)
      {
        result = Expression.Condition(
          Expression.Equal(members[^i], Expression.Constant(null)),
          Expression.Default(property.PropertyType),
          result);
      }

      return result;
    });
  }
}
