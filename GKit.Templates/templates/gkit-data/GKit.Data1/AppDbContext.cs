using GKit.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace GKit.Data1;

public class AppDbContext(DbContextOptions<AppDbContext> options)
  : DbContext(options), ISoftDeleteAwareContext
{
  public SoftDeleteInterceptor SoftDeleteInterceptor { get; } = new();

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.AddInterceptors(SoftDeleteInterceptor);

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
  }
}
