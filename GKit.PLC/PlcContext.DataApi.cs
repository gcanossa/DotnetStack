using S7.Net.Types;

namespace GKit.PLC;

public abstract partial class PlcContext
{
    public async Task<object?[]> ReadValuesAsync(IEnumerable<string> addresses, CancellationToken ct = default)
    {
        var nodes = await ReadItemsAsync(addresses.Select(DataItem.FromAddress), ct);

        return nodes.Select(p => p.Value).ToArray();
    }

    public async Task WriteValuesAsync(Dictionary<string, object> values, CancellationToken ct = default)
    {
        var results = await WriteItemsAsync(values.Select(p => DataItem.FromAddressAndValue(p.Key, p.Value)), ct);

        var firstError = results.FirstOrDefault(p => p != null);
        if (firstError != null)
            throw firstError;
    }
}