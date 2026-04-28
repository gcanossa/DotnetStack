using System.Linq.Expressions;
using System.Reflection;
using S7.Net;
using S7.Net.Types;
using DateTime = System.DateTime;

namespace GKit.PLC;

public interface IEntityTypeBuilder<T> where T : class
{
    public IPropertyBuilder<TProperty> Property<TProperty>(Expression<Func<T, TProperty>> expression);
}

internal class EntityTypeBuilder<T>(Dictionary<PropertyInfo, EntityPropertyDescriptor> propertyDescriptors) : IEntityTypeBuilder<T> where T : class
{
    private static VarType FromClrType(Type clrType)
    {
        if (clrType == typeof(byte) || typeof(IEnumerable<byte>).IsAssignableFrom(clrType))
            return VarType.Byte;

        if (clrType == typeof(short) || typeof(IEnumerable<short>).IsAssignableFrom(clrType))
            return VarType.Word;
        
        if (clrType == typeof(int) || typeof(IEnumerable<int>).IsAssignableFrom(clrType))
            return VarType.DWord;

        if (clrType == typeof(long) || typeof(IEnumerable<long>).IsAssignableFrom(clrType))
            return VarType.DInt;

        if (clrType == typeof(float) || typeof(IEnumerable<float>).IsAssignableFrom(clrType))
            return VarType.Real;

        if (clrType == typeof(double) || typeof(IEnumerable<double>).IsAssignableFrom(clrType))
            return VarType.LReal;
        
        if (clrType == typeof(string) || typeof(IEnumerable<string>).IsAssignableFrom(clrType))
            return VarType.String;
        
        if (clrType == typeof(DateTime) || typeof(IEnumerable<DateTime>).IsAssignableFrom(clrType))
            return VarType.DateTimeLong;

        return VarType.DWord;
    }
    
    public IPropertyBuilder<TProperty> Property<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        var propertyInfo = (PropertyInfo)((MemberExpression)expression.Body).Member;
        if(propertyInfo == null)
            throw new ArgumentException($"Expression '{expression}' refers to a method, not a property.");

        if (!propertyDescriptors.ContainsKey(propertyInfo))
        {
            propertyDescriptors.Add(propertyInfo, new EntityPropertyDescriptor() { DataItem = new DataItem()
            {
                VarType = FromClrType(propertyInfo.PropertyType),
            } });
        }
        
        return new PropertyBuilder<TProperty>(propertyDescriptors[propertyInfo]);
    }
}

public interface IPropertyBuilder<T>
{
    public IPropertyDetailsBuilder<T> ToPlcAddress(string address);

    public IPropertyCoordinatesBuilder<T> ToDb(int dbNumber);
}

public interface IPropertyCoordinatesBuilder<T>
{
    public IPropertyDetailsBuilder<T> WithCoordinates(int startByte, byte bitAddress);
}

public interface IPropertyDetailsBuilder<T>
{
    public void Having(int count);
    public void Having(int count, VarType varType);
    public void Having(int count, VarType varType, DataType dataType);
}

internal class PropertyBuilder<T>(EntityPropertyDescriptor descriptor) : IPropertyBuilder<T>
{
    public IPropertyCoordinatesBuilder<T> ToDb(int dbNumber)
    {
        descriptor.DataItem.DB = dbNumber;
        
        return new PropertyCoordinatesBuilder<T>(descriptor);
    }


    IPropertyDetailsBuilder<T> IPropertyBuilder<T>.ToPlcAddress(string address)
    {
        var prev = descriptor.DataItem;
        descriptor.DataItem = DataItem.FromAddress(address);
        descriptor.DataItem.VarType = prev.VarType;
        
        return new PropertyDetailsBuilder<T>(descriptor);
    }
}

internal class PropertyCoordinatesBuilder<T>(EntityPropertyDescriptor descriptor) : IPropertyCoordinatesBuilder<T>
{
    public IPropertyDetailsBuilder<T> WithCoordinates(int startByte, byte bitAddress)
    {
        descriptor.DataItem.StartByteAdr  = startByte;
        descriptor.DataItem.BitAdr = bitAddress;
        
        return new PropertyDetailsBuilder<T>(descriptor);
    }
}

internal class PropertyDetailsBuilder<T>(EntityPropertyDescriptor descriptor) : IPropertyDetailsBuilder<T>
{
    public void Having(int count)
    {
        descriptor.DataItem.Count = count;
    }

    public void Having(int count, VarType varType)
    {
        this.Having(count);
        descriptor.DataItem.VarType = varType;
    }

    public void Having(int count, VarType varType, DataType dataType)
    {
        this.Having(count, varType);
        descriptor.DataItem.DataType = dataType;
    }
}

internal class EntityPropertyDescriptor
{
    public required DataItem DataItem { get; set; }
}

public interface IModelBuilder
{
    public IEntityTypeBuilder<T> Entity<T>() where T : class;
}

internal class ModelBuilder : IModelBuilder
{
    internal Dictionary<Type, Dictionary<PropertyInfo, EntityPropertyDescriptor>> EntityModels { get; } = new ();
    
    public IEntityTypeBuilder<T> Entity<T>() where T : class
    {
        if (!EntityModels.ContainsKey(typeof(T)))
        {
            EntityModels.Add(typeof(T), new Dictionary<PropertyInfo, EntityPropertyDescriptor>());
        }

        return new EntityTypeBuilder<T>(EntityModels[typeof(T)]);
    }
}