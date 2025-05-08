using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GKit.EntityFramework;

public static class SoftDeleteExtensions
{
  public static void Remove<T, E>(this T context, E entity, bool hard = false)
    where T : DbContext, ISoftDeleteAwareContext
    where E : class, ISoftDeletableEntity
  {
    if (hard)
    {
      context.SoftDeleteInterceptor.RegisterForHardDelete(entity);
    }

    context.Remove(entity);
  }

  public static EntityTypeBuilder<T> WithSoftDelete<T>(this EntityTypeBuilder<T> builder) where T : class, ISoftDeletableEntity
  {
    return builder.HasQueryFilter(p => p.DeletedAt == null);
  }
}
