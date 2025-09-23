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

    protected TOptions Options => options.Value;
    protected IDbContextFactory<TContext> DbContextFactory => dbContextFactory;

    public override bool CanUpdate => true;

    public override async Task<TOptions> GetOptionsAsync(CancellationToken ct = default)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(ct);

        var props = await context.Set<Setting>().Where(p => p.Key.StartsWith(BaseKey)).ToListAsync(ct);

        var newOptions = new TOptions();
        foreach (var prop in typeof(TOptions).GetProperties())
        {
            prop.SetValue(newOptions, prop.GetValue(Options), null);
        }

        foreach (var item in props)
        {
            var prop = typeof(TOptions).GetProperty(item.Key.Replace($"{BaseKey}:", string.Empty));
            if (prop is null) continue;

            if (item.Value != null)
                prop.SetValue(newOptions,
                    prop.PropertyType == typeof(TimeSpan)
                        ? TimeSpan.Parse(item.Value)
                        : Convert.ChangeType(item.Value, prop.PropertyType));
        }

        return newOptions;
    }

    protected virtual IEnumerable<PropertyInfo> SelectProperties()
    {
        return typeof(TOptions).GetProperties();
    }

    protected override async Task SaveOptionsAsync(TOptions savingOptions, CancellationToken ct = default)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(ct);

        foreach (var prop in SelectProperties())
        {
            var key = $"{BaseKey}:{prop.Name}";
            var entity = await context.Set<Setting>().FirstOrDefaultAsync(p => p.Key == key, ct) ?? (await context.AddAsync(
                new Setting() { Key = key, TypeName = prop.PropertyType.AssemblyQualifiedName! }, ct)).Entity;

            entity.Value = prop.GetValue(savingOptions)?.ToString() ?? "";
        }

        await context.SaveChangesAsync(ct);
    }
}