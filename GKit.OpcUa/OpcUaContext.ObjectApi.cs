using System.Reflection;
using Opc.Ua;

namespace GKit.OpcUa;

public abstract partial class OpcUaContext
{
    public async Task<T> ReadObjectAsync<T>() where T : class, new()
    {
        if(!EntityModels.TryGetValue(typeof(T), out var entityProperties))
            throw new ArgumentException($"The type {typeof(T).FullName} is not registered.");
        
        if(entityProperties.Count == 0)
            throw new ArgumentException($"The type {typeof(T).FullName} has no properties.");

        var mappings = entityProperties.ToList();

        var values = await ReadValuesAsync(mappings.Select(p => p.Value.NodeId));
        
        var result = new T();

        for (var i = 0; i < mappings.Count; i++)
        {
            mappings[i].Key.SetValue(result, values[i]);
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

        await WriteValuesAsync(entityProperties.ToDictionary(
            p => p.Value.NodeId, 
            p => p.Key.GetValue(value))!);
    }
}