using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GKit.EntityFramework;

public static class RevisionExtensions
{
  public static void Update<T, E>(this T context, E entity, bool noRevision = false)
    where T : DbContext, IRevisionAwareContext
    where E : class, IRevisionableEntity
  {
    if (noRevision)
    {
      context.RevisionInterceptor.RegisterForUpdate(entity);
    }

    context.Update(entity);
  }

  public static ComplexPropertyBuilder<RevisionInfo> WithRevision<T>(this EntityTypeBuilder<T> builder) where T : class, IRevisionableEntity
  {
    return builder.ComplexProperty(p => p.Revision);
  }
}
