using System.Globalization;
using GKit.App1;
using GKit.Application;
using GKit.BlazorExt;
using GKit.App1.Components;
#if (cliRunners)
using GKit.App1.CliRunners;
#endif
#if (db_any)
using GKit.App1.Data;
using Microsoft.EntityFrameworkCore;
#endif
#if (ui_any)
using GKit.UI.Localization;
#endif
#if (ui_mudblazor)
using GKit.UI.MudBlazorExt;
using MudBlazor;
using MudBlazor.Services;
#endif
#if (ui_radzen)
using GKit.UI.RadzenExt;
using Radzen;
#endif
#if (auth_simple)
using GKit.Authentication.Simple;
#endif
#if (auth_ad)
using GKit.Authentication.ActiveDirectory;
#endif
#if (auth_entra)
using Microsoft.Identity.Web;
#endif
#if (quartz)
using GKit.Quartz;
#endif
#if (pdf)
using GKit.Pdf;
#endif
#if (rentri)
using GKit.RENTRI;
#endif
#if (smartcard)
using GKit.SmartCardHost;
#endif
using Serilog;

// Application.Wrap installs a bootstrap logger, runs any ICommandLineRunner matching argv and
// only then starts the host, so --help and one-off commands work without a running web server.
Application.Wrap(args, () =>
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

#if (host_windows)
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddWindowsService();
    }
#endif
#if (host_systemd)
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddSystemd();
    }
#endif

    builder.Services.AddLocalization();
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("GKIT-DEFAULT-CULTURE");

#if (ui_mudblazor)
    builder.Services.AddMudServices(options =>
    {
        options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    });
    builder.Services.AddGKitMudBlazorUi();
#endif
#if (ui_radzen)
    builder.Services.AddRadzenComponents();
    builder.Services.AddGKitRadzenUi();
#endif
#if (ui_any)
    // The GKit libraries default to neutral English; referencing GKit.UI.Localization is what
    // opts this application into the translated strings.
    builder.Services.AddGKitUiLocalization();
#endif

    builder.Services.AddGKitBlazorServices();

#if (db_any)
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                               throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var dbOptions = builder.Environment.EnvironmentName switch
        {
#if (db_sqlserver)
            _ => options.UseSqlServer(connectionString,
                b => b.MigrationsAssembly(typeof(AppSettings).Assembly.FullName).EnableRetryOnFailure()),
#endif
#if (db_npgsql)
            _ => options.UseNpgsql(connectionString,
                b => b.MigrationsAssembly(typeof(AppSettings).Assembly.FullName)),
#endif
#if (db_mysql)
            _ => options.UseMySQL(connectionString,
                b => b.MigrationsAssembly(typeof(AppSettings).Assembly.FullName)),
#endif
#if (db_sqlite)
            _ => options.UseSqlite(connectionString,
                b => b.MigrationsAssembly(typeof(AppSettings).Assembly.FullName)),
#endif
        };
    });
#endif

#if (auth_simple)
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddSimpleAuthentication<ApplicationDbContext, ApplicationUser>(
        simpleConfig: options =>
        {
            // Accounts filtered out here can still be managed, only not signed in.
            options.SignInQuery = q => q.Where(p => p.IsActive);
        });
#endif
#if (auth_ad)
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddActiveDirectoryAuthentication();
#endif
#if (auth_entra)
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAd");
#endif
#if (auth_any)
    builder.Services.AddAuthorization();
#endif

#if (pdf)
    builder.Services.AddGKitPdfServices();
#endif
#if (rentri)
    builder.Services.AddRentriServices();
#endif
#if (quartz)
    builder.Services.AddGKitQuartz();
#endif
#if (smartcard)
    builder.Services.AddGKitSmartCardHost();
#endif

#if (cliRunners)
    // Register one ICommandLineRunner per command; --help lists them.
    // builder.Services.AddSingleton<ICommandLineRunner, MyCommandLineRunner>();
#endif

#if (health)
    var healthChecks = builder.Services.AddHealthChecks();
#if (db_any)
    healthChecks.AddDbContextCheck<ApplicationDbContext>("APP_DB");
#endif
#if (quartz)
    healthChecks.AddQuartzCheck("SCHEDULER");
#endif
#if (rentri)
    healthChecks.AddRentriCheck("RENTRI_API");
#endif
#if (smartcard)
    healthChecks.AddSmartCardHostCheck("SMART_CARD");
#endif
#endif

    if (!builder.Environment.IsDevelopment())
    {
        builder.WebHost.UseStaticWebAssets();
    }

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    app.UseRequestLocalization(new RequestLocalizationOptions()
        .AddSupportedCultures(GKIT-SUPPORTED-CULTURES)
        .AddSupportedUICultures(GKIT-SUPPORTED-CULTURES));

    app.UseHttpsRedirection();

    app.UseStaticFiles();

#if (auth_any)
    app.UseAuthentication();
    app.UseAuthorization();
#endif

    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

#if (health)
    app.MapHealthChecksJson("/api/health");
#endif
#if (smartcard)
    app.MapGKitSmartCardHost();
#endif
#if (quartz)
    app.UseGKitQuartz();
#endif
#if (db_any)
    // No-op in Development, so a developer keeps control of when migrations run.
    app.ApplyPendingMigrations<ApplicationDbContext>();
#endif

    return app;
});
