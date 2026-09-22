using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GKit.EntityFramework;

public static class RevisionExtensions
{
  /// <summary>
  /// An <c>Expression&lt;Func&lt;T, object&gt;&gt;</c> boxes value types, so <c>p =&gt; p.Id</c>
  /// for an <c>int Id</c> arrives as <c>Convert(p.Id, Object)</c> rather than a
  /// <see cref="MemberExpression"/>. Unwrap the conversion before reading the member, otherwise
  /// every exclusion of a value-typed property — including the primary keys this API exists to
  /// protect — is silently ignored.
  /// </summary>
  private static Expression Unwrap(Expression expression) =>
    expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
      ? unary.Operand
      : expression;

  private static IEnumerable<string> ExcludedNames<T>(Expression<Func<T, object>>? exclude)
  {
    if (exclude is null) yield break;

    switch (Unwrap(exclude.Body))
    {
      case NewExpression newExp:
        // Read the constructor arguments rather than Members: an anonymous type's members can
        // be renamed (`new { Key = p.Id }`), but the argument still points at the property.
        foreach (var argument in newExp.Arguments)
          if (Unwrap(argument) is MemberExpression member)
            yield return member.Member.Name;
        break;

      case MemberExpression memberExp:
        yield return memberExp.Member.Name;
        break;
    }
  }

  public static T CopyToObject<T>(this T from, T to, Expression<Func<T, object>>? exclude = null, IEnumerable<string>? excludeProps = null) where T : class
  {
    HashSet<string> excludedPropNames = [.. excludeProps ?? [], .. ExcludedNames(exclude)];

    var properties = typeof(T).GetProperties()
      .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
      .Where(p => !excludedPropNames.Contains(p.Name));

    foreach (var prop in properties)
    {
      prop.SetValue(to, prop.GetValue(from));
    }

    return to;
  }
  
  public static T CopyTo<T>(this T from, T to, Expression<Func<T, object>>? exclude = null) where T : class, IRevisionableEntity
  {
    to.Revision = from.Revision.Clone();

    List<string> excludedPropNames = [nameof(from.Revision)];

    from.CopyToObject(to, exclude, excludedPropNames);

    return to;
  }

  public static T CopyFromObject<T>(this T to, T from, Expression<Func<T, object>>? exclude = null, IEnumerable<string>? excludeProps = null) where T : class
  {
    return from.CopyToObject(to, exclude, excludeProps);
  }

  public static T CopyFrom<T>(this T to, T from, Expression<Func<T, object>>? exclude = null) where T : class, IRevisionableEntity
  {
    return from.CopyTo(to, exclude);
  }

  public static void DisableRevisions<T, E>(this T context, E entity)
    where T : DbContext, IRevisionAwareContext
    where E : class, IRevisionableEntity
  {
    context.RevisionInterceptor.RegisterForUpdate(context, entity);
  }

  public static void Update<T, E>(this T context, E entity, bool noRevision = false)
    where T : DbContext, IRevisionAwareContext
    where E : class, IRevisionableEntity
  {
    if (noRevision)
    {
      context.DisableRevisions(entity);
    }

    context.Update(entity);
  }
  
  public static void UpdateRevisionReferences<T, E>(this T context, E entity)
    where T : DbContext, IRevisionAwareContext
    where E : class
  {
    context.RevisionInterceptor.RegisterForRevisionReferenceUpdate(context, entity);
  }

  public static ComplexPropertyBuilder<RevisionInfo> WithRevision<T>(this EntityTypeBuilder<T> builder) where T : class, IRevisionableEntity
  {
    return builder.ComplexProperty(p => p.Revision);
  }
}
