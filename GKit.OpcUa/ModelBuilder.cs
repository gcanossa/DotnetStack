using System.Linq.Expressions;
using System.Reflection;

namespace GKit.OpcUa;

public interface IEntityTypeBuilder<T> where T : class
{
    public IPropertyBuilder<TProperty> Property<TProperty>(Expression<Func<T, TProperty>> expression);
}

internal class EntityTypeBuilder<T>(Dictionary<PropertyInfo, EntityPropertyDescriptor> propertyDescriptors)
    : IEntityTypeBuilder<T> where T : class
{
    public IPropertyBuilder<TProperty> Property<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        var propertyInfo = (PropertyInfo)((MemberExpression)expression.Body).Member;
        if (propertyInfo == null)
            throw new ArgumentException($"Expression '{expression}' refers to a method, not a property.");

        if (!propertyDescriptors.ContainsKey(propertyInfo))
        {
            propertyDescriptors.Add(propertyInfo,
                new EntityPropertyDescriptor() { NodeId = $"ns=0;s={propertyInfo.Name}" });
        }

        return new PropertyBuilder<TProperty>(propertyDescriptors[propertyInfo]);
    }
}

public interface IPropertyBuilder<T>
{
    public IPropertyHasConversionBuilder<T> ToNodeId(string nodeId);
}

internal class PropertyBuilder<T>(EntityPropertyDescriptor descriptor) : IPropertyBuilder<T>
{
    public IPropertyHasConversionBuilder<T> ToNodeId(string nodeId)
    {
        descriptor.NodeId = nodeId;
        return new PropertyHasConversionBuilder<T>(descriptor);
    }
}

public interface IPropertyHasConversionBuilder<T>
{
    public void HasConversion(ValueConverter converter);
}

internal class PropertyHasConversionBuilder<T>(EntityPropertyDescriptor descriptor) : IPropertyHasConversionBuilder<T>
{
    public void HasConversion(ValueConverter converter)
    {
        descriptor.ValueConverter = converter;
    }
}

internal class EntityPropertyDescriptor
{
    public required string NodeId { get; set; }
    public ValueConverter? ValueConverter { get; set; }
}

public interface IModelBuilder
{
    public IEntityTypeBuilder<T> Entity<T>() where T : class;
}

internal class ModelBuilder : IModelBuilder
{
    internal Dictionary<Type, Dictionary<PropertyInfo, EntityPropertyDescriptor>> EntityModels { get; } = new();

    public IEntityTypeBuilder<T> Entity<T>() where T : class
    {
        if (!EntityModels.ContainsKey(typeof(T)))
        {
            EntityModels.Add(typeof(T), new());
        }

        return new EntityTypeBuilder<T>(EntityModels[typeof(T)]);
    }
}

public class ValueConverter(Func<object?, object?> toProvider, Func<object?, object?> fromProvider)
{
    public Func<object?, object?> FromProvider => fromProvider;
    public Func<object?, object?> ToProvider => toProvider;
}

public class ValueConverter<TModel, TProvider>(
    Func<TModel, TProvider> toProvider,
    Func<TProvider, TModel> fromProvider)
    : ValueConverter(p => toProvider((TModel)p!), p => fromProvider((TProvider)p!))
{
}