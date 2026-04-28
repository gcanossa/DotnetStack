using System.Reflection;
using S7.Net.Types;

namespace GKit.PLC;

public abstract partial class PlcContext
{
    private static DataItem CloneDataItem(DataItem value, object? dataValue = null)
    {
        return new DataItem()
        {
            DataType = value.DataType,
            VarType = value.VarType,
            DB = value.DB,
            StartByteAdr = value.StartByteAdr,
            BitAdr = value.BitAdr,
            Count = value.Count,
            
            Value = dataValue,
        };
    }
    public async Task<T> ReadObjectAsync<T>() where T : class, new()
    {
        if(!EntityModels.TryGetValue(typeof(T), out var entityProperties))
            throw new ArgumentException($"The type {typeof(T).FullName} is not registered.");
        
        if(entityProperties.Count == 0)
            throw new ArgumentException($"The type {typeof(T).FullName} has no properties.");

        var mappings = entityProperties.ToList();

        var values = (await ReadItemsAsync(mappings
            .Select(p => CloneDataItem(p.Value.DataItem)))).ToArray();
        
        var result = new T();

        for (var i = 0; i < mappings.Count; i++)
        {
            mappings[i].Key.SetValue(result, values[i].Value);
        }
        
        return result;
    }
    
    public async Task WriteObjectAsync<T>(T value) where T : class
    {
        if(!EntityModels.TryGetValue(typeof(T), out var entityProperties))
            throw new ArgumentException($"The type {typeof(T).FullName} is not registered.");
        
        if(entityProperties.Count == 0)
            throw new ArgumentException($"The type {typeof(T).FullName} has no properties.");

        var mappings = entityProperties.ToList();

        await WriteItemsAsync(entityProperties.Select(p => CloneDataItem(p.Value.DataItem, p.Key.GetValue(value))));
    }
}