namespace GKit.Settings;

public abstract class SettingsManager<TOptions> where TOptions : class, new()
{
    public event Func<TOptions, Task>? OptionsChanged;

    public abstract Task<TOptions> GetOptionsAsync(CancellationToken ct = default);

    public abstract bool CanUpdate { get; }

    public virtual async Task UpdateOptionsAsync(TOptions options, CancellationToken ct = default)
    {
        await SaveOptionsAsync(options, ct);

        // A multicast Func<T, Task> returns only the *last* handler's task from Invoke(), so
        // earlier handlers would be started and never awaited — their failures unobservable and
        // their work still in flight when UpdateOptionsAsync returns.
        if (OptionsChanged is null) return;

        await Task.WhenAll(OptionsChanged.GetInvocationList()
            .Cast<Func<TOptions, Task>>()
            .Select(handler => handler(options)));
    }

    protected abstract Task SaveOptionsAsync(TOptions options, CancellationToken ct = default);
}