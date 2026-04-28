using S7.Net.Types;

namespace GKit.PLC;

public abstract partial class PlcContext
{
    public async Task<IEnumerable<DataItem>> ReadItemsAsync(IEnumerable<DataItem> data, CancellationToken ct = default)
    {
        return await GuardRequestAsync(async () =>
        {
            var result = data.ToList();
            
            var response = await Connection!.ReadMultipleVarsAsync(
                result,
                ct).ConfigureAwait(false);

            return response;
        }, ct);
    }

    public async Task<IEnumerable<Exception?>> WriteItemsAsync(IEnumerable<DataItem> values, CancellationToken ct = default)
    {
        return await GuardRequestAsync(async () =>
        {
            var result = await Task.WhenAll(values
                .Select(async value =>
                {
                    try
                    {
                        await Connection!.WriteAsync(value.DataType, value.DB, value.StartByteAdr, value.Value!, value.BitAdr,
                            ct);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                }));

            return result;
        }, ct);
    }
}