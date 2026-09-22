using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GKit.Settings;

public class DbSettingsManager<TOptions, TContext>(
    IOptions<TOptions> options,
    IDbContextFactory<TContext> dbContextFactory) : SettingsManager<TOptions>
    where TOptions : class, new()
    where TContext : DbContext
{

    protected string BaseKey => typeof(TOptions).Name;

    /// <summary>
    /// The key prefix including its separator. Matching on <see cref="BaseKey"/> alone also
    /// matched every options type whose name merely starts with it ("AppOptions" vs
    /// "AppOptionsAdvanced").
    /// </summary>
    private string KeyPrefix => $"{BaseKey}:";

    protected TOptions Options => options.Value;
    protected IDbContextFactory<TContext> DbContextFactory => dbContextFactory;

    public override bool CanUpdate => true;

    public override async Task<TOptions> GetOptionsAsync(CancellationToken ct = default)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(ct);

        var props = await context.Set<Setting>().Where(p => p.Key.StartsWith(KeyPrefix)).ToListAsync(ct);

        var newOptions = new TOptions();

        // Computed / get-only properties have no setter; copying them would throw.
        foreach (var prop in WritableProperties())
        {
            prop.SetValue(newOptions, prop.GetValue(Options), null);
        }

        foreach (var item in props)
        {
            var prop = typeof(TOptions).GetProperty(item.Key[KeyPrefix.Length..]);
            if (prop is null || !prop.CanWrite) continue;

            if (item.Value != null)
                prop.SetValue(newOptions, SettingValueConverter.FromStorage(item.Value, prop.PropertyType));
        }

        return newOptions;
    }

    private static IEnumerable<PropertyInfo> WritableProperties() =>
        typeof(TOptions).GetProperties()
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

    protected virtual IEnumerable<PropertyInfo> SelectProperties()
    {
        return WritableProperties();
    }

    protected override async Task SaveOptionsAsync(TOptions savingOptions, CancellationToken ct = default)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(ct);

        foreach (var prop in SelectProperties())
        {
            var key = $"{KeyPrefix}{prop.Name}";
            var entity = await context.Set<Setting>().FirstOrDefaultAsync(p => p.Key == key, ct) ?? (await context.AddAsync(
                new Setting() { Key = key, TypeName = prop.PropertyType.AssemblyQualifiedName! }, ct)).Entity;

            entity.Value = SettingValueConverter.ToStorage(prop.GetValue(savingOptions));
        }

        await context.SaveChangesAsync(ct);
    }
}