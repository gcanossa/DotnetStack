using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.Settings;

public static class GKitSettingsExtensions
{
  /// <summary>
  /// Configures the <see cref="Setting"/> entity.
  /// <para>
  /// None of <see cref="Setting"/>'s properties is a primary key by EF convention ("Key" is
  /// neither "Id" nor "SettingId"), so <c>context.Set&lt;Setting&gt;()</c> previously failed
  /// model building until every consumer hand-wrote this.
  /// </para>
  /// </summary>
  public static ModelBuilder AddGKitSettings(this ModelBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.Entity<Setting>(entity =>
    {
      entity.HasKey(p => p.Key);
      entity.Property(p => p.Key).HasMaxLength(256);
      entity.Property(p => p.TypeName).HasMaxLength(512);
    });

    return builder;
  }

  /// <summary>
  /// Registers a database-backed <see cref="SettingsManager{TOptions}"/> that overlays stored
  /// values on top of the <paramref name="configurationSection"/> defaults.
  /// </summary>
  public static IServiceCollection AddDbSettings<TOptions, TContext>(
    this IServiceCollection services, string configurationSection)
    where TOptions : class, new()
    where TContext : DbContext
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddOptions<TOptions>().BindConfiguration(configurationSection);
    services.AddSingleton<SettingsManager<TOptions>, DbSettingsManager<TOptions, TContext>>();

    return services;
  }

  /// <summary>
  /// Registers a read-only <see cref="SettingsManager{TOptions}"/> backed purely by configuration.
  /// </summary>
  public static IServiceCollection AddDefaultSettings<TOptions>(
    this IServiceCollection services, string configurationSection)
    where TOptions : class, new()
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddOptions<TOptions>().BindConfiguration(configurationSection);
    services.AddSingleton<SettingsManager<TOptions>, DefaultSettingsManager<TOptions>>();

    return services;
  }
}
