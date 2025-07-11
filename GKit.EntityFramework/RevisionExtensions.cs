using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GKit.EntityFramework;

public static class RevisionExtensions
{
  public static T CopyTo<T>(this T from, T to, Expression<Func<T, object>>? exclude = null) where T : class, IRevisionableEntity
  {
    to.Revision = from.Revision.Clone();

    List<string> excludedPropNames = [nameof(from.Revision)];

    if (exclude != null)
    {
      if (exclude.Body is NewExpression newExp)
      {
        excludedPropNames.AddRange(newExp.Members?.Select(p => p.Name) ?? []);
      }
      else if (exclude.Body is MemberExpression memberExp)
      {
        excludedPropNames.Add(memberExp.Member.Name);
      }
    }

    foreach (var prop in typeof(T).GetProperties().Where(p => !excludedPropNames.Contains(p.Name)))
    {
      prop.SetValue(to, prop.GetValue(from));
    }

    return to;
  }

  public static T CopyFrom<T>(this T to, T from, Expression<Func<T, object>>? exclude = null) where T : class, IRevisionableEntity
  {
    return from.CopyTo(to, exclude);
  }

  public static void DisableRevisions<T, E>(this T context, E entity)
    where T : DbContext, IRevisionAwareContext
    where E : class, IRevisionableEntity
  {
    context.RevisionInterceptor.RegisterForUpdate(entity);
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

  public static ComplexPropertyBuilder<RevisionInfo> WithRevision<T>(this EntityTypeBuilder<T> builder) where T : class, IRevisionableEntity
  {
    return builder.ComplexProperty(p => p.Revision);
  }
}
