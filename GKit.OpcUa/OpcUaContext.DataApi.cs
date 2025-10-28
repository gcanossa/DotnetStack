using Opc.Ua;

namespace GKit.OpcUa;

public abstract partial class OpcUaContext
{
    public async Task<object[]> ReadValuesAsync(IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        var nodes = await ReadNodesAsync(nodeIds.Select(p => new ReadValueId()
        {
            NodeId = p,
            AttributeId = Attributes.Value
        }), ct);

        return nodes.ReadData();
    }

    public async Task WriteValuesAsync(Dictionary<string, object> values, CancellationToken ct = default)
    {
        var results = await WriteNodesAsync(values.Select(p => new WriteValue()
        {
            NodeId = p.Key,
            AttributeId = Attributes.Value,
            Value = new DataValue { Value = p.Value }
        }), ct);
        
        results.ThrowIfAnyError();
    }
}