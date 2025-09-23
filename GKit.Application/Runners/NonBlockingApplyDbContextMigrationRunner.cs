using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace GKit.Application.Runners;

public class NonBlockingApplyDbContextMigrationRunner<T> : ICommandLineRunner where T : DbContext
{
    public bool ShouldDisableHostRun(IHost host, string[] args) => false;
    public bool Matches(IHost host, string[] args) => args.Any(p => p == "--apply-migrations");

    public async Task Execute(IHost host, string[] args) => await host.ExecuteApplyPendingMigrations<T>();
}