using Microsoft.EntityFrameworkCore;

namespace GKit.EntityFramework;

public interface ISoftDeleteAwareContext
{
  public SoftDeleteInterceptor SoftDeleteInterceptor { get; }
}
