namespace GKit.Settings;

public abstract class SettingsManager<TOptions> where TOptions : class, new()
{
    public event Func<TOptions, Task>? OptionsChanged;

    public abstract Task<TOptions> GetOptionsAsync(CancellationToken ct = default);

    public abstract bool CanUpdate { get; }

    public virtual async Task UpdateOptionsAsync(TOptions options, CancellationToken ct = default)
    {
        await SaveOptionsAsync(options, ct);
        if (OptionsChanged is not null)
            await OptionsChanged.Invoke(options);
    }

    protected abstract Task SaveOptionsAsync(TOptions options, CancellationToken ct = default);
}