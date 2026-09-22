using GKit.Application;
using GKit.DbMigration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// A data migration is a long, resumable, one-off command rather than a service, so it runs through
// ICommandLineRunner: Application.Wrap starts the host, runs the matching runner and skips
// host.Run().
Application.Wrap(args, () =>
{
    var builder = Host.CreateApplicationBuilder(args);

    // Host.CreateApplicationBuilder(args) already loads appsettings.json and
    // appsettings.{Environment}.json, so there is nothing to add here.

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // The mappings store keeps the legacy-key to new-key correspondence on disk, so a migration
    // interrupted halfway resumes instead of duplicating rows.
    builder.Services.AddScoped<IMigrationMappingsStore, FileSystemMigrationMappingsStore>();

    // Register the source and destination contexts, then the migration context binding them:
    //
    //   builder.Services.AddDbContextFactory<LegacyDbContext>(...);
    //   builder.Services.AddDbContextFactory<AppDbContext>(...);
    //   builder.Services.AddScoped<DbMigrationContext<LegacyDbContext, AppDbContext>, AppMigrationContext>();

    return builder.Build();
});
