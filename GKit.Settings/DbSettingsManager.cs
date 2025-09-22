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

    protected readonly string _baseKey = typeof(TOptions).Name;

    protected readonly TOptions _options = options.Value;
    protected readonly IDbContextFactory<TContext> _dbContextFactory = dbContextFactory;

    public override bool CanUpdate => true;

    public override async Task<TOptions> GetOptionsAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();

        var props = await context.Set<Setting>().Where(p => p.Key.StartsWith(_baseKey)).ToListAsync();

        var options = new TOptions();
        foreach (var prop in typeof(TOptions).GetProperties())
        {
            prop.SetValue(options, prop.GetValue(_options), null);
        }

        foreach (var item in props)
        {
            var prop = typeof(TOptions).GetProperty(item.Key.Replace($"{_baseKey}:", string.Empty));
            if (prop is not null)
            {
                if (prop.PropertyType == typeof(TimeSpan))
                    prop!.SetValue(options, TimeSpan.Parse(item.Value));
                else
                    prop!.SetValue(options, Convert.ChangeType(item.Value, prop.PropertyType));
            }
        }

        return options;
    }

    protected virtual IEnumerable<PropertyInfo> SelectProperties()
    {
        return typeof(TOptions).GetProperties();
    }

    protected override async Task SaveOptionsAsync(TOptions options)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();

        foreach (var prop in SelectProperties())
        {
            var key = $"{_baseKey}:{prop.Name}";
            var entity = await context.Set<Setting>().FindAsync(key) ?? (await context.AddAsync(
                new Setting() { Key = key, TypeName = prop.PropertyType.AssemblyQualifiedName! })).Entity;

            entity.Value = prop.GetValue(options)?.ToString() ?? "";
        }

        await context.SaveChangesAsync();
    }
}