namespace GKit.Settings;

public abstract class SettingsManager<TOptions> where TOptions : class, new()
{
    public event Func<TOptions, Task>? OptionsChanged;

    public abstract Task<TOptions> GetOptionsAsync();

    public abstract bool CanUpdate { get; }

    public virtual async Task UpdateOptionsAsync(TOptions options)
    {
        await SaveOptionsAsync(options);
        if (OptionsChanged is not null)
            await OptionsChanged.Invoke(options);
    }

    protected abstract Task SaveOptionsAsync(TOptions options);
}