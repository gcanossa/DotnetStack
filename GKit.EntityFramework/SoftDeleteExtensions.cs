using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GKit.EntityFramework;

public static class SoftDeleteExtensions
{
  public static void DisableSoftDelete<T, E>(this T context, E entity, bool hard = false)
    where T : DbContext, ISoftDeleteAwareContext
    where E : class, ISoftDeletableEntity
  {
    context.SoftDeleteInterceptor.RegisterForHardDelete(entity);
  }

  public static void Remove<T, E>(this T context, E entity, bool hard = false)
    where T : DbContext, ISoftDeleteAwareContext
    where E : class, ISoftDeletableEntity
  {
    if (hard)
    {
      context.DisableSoftDelete(entity);
    }

    context.Remove(entity);
  }

  public static EntityTypeBuilder<T> WithSoftDelete<T>(this EntityTypeBuilder<T> builder) where T : class, ISoftDeletableEntity
  {
    return builder.HasQueryFilter(p => p.DeletedAt == null);
  }
}
