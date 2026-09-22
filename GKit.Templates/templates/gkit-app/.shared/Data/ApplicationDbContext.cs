using GKit.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace GKit.App1.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), ISoftDeleteAwareContext
{
    public SoftDeleteInterceptor SoftDeleteInterceptor { get; } = new();

#if (auth_simple)
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
#endif

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(SoftDeleteInterceptor);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

#if (auth_simple)
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(p => p.AccountName).IsUnique();
            entity.WithSoftDelete();
        });
#endif
    }
}
