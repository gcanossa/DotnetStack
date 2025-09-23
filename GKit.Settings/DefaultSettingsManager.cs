using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GKit.Settings;

public class DefaultSettingsManager<TOptions>(IOptions<TOptions> options) : SettingsManager<TOptions>
    where TOptions : class, new()
{
    public override Task<TOptions> GetOptionsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(options.Value);
    }

    public override bool CanUpdate => false;
    protected override Task SaveOptionsAsync(TOptions value, CancellationToken ct = default)
    {
        throw new NotSupportedException();
    }
}