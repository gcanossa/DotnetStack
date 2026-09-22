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

internal class EntityTypeBuilder<T>(Dictionary<PropertyInfo, EntityPropertyDescriptor> propertyDescriptors)
    : IEntityTypeBuilder<T> where T : class
{
    /// <summary>
    /// Maps a CLR type onto the S7 <see cref="VarType"/> with the same width *and signedness*.
    /// <para>
    /// Getting signedness wrong is silent data corruption, not an error: a DInt holding -1 read
    /// as a DWord comes back as 4294967295. Returns <see langword="null"/> rather than guessing
    /// when there is no faithful mapping — the caller then requires the type to be pinned by the
    /// PLC address or by <c>Having(count, varType)</c>.
    /// </para>
    /// </summary>
    internal static VarType? FromClrType(Type clrType)
    {
        var elementType = ElementTypeOf(clrType);

        // An enum is stored as its underlying integer; the PLC knows nothing about the names.
        if (elementType.IsEnum) elementType = Enum.GetUnderlyingType(elementType);

        if (elementType == typeof(bool)) return VarType.Bit;
        if (elementType == typeof(byte)) return VarType.Byte;
        if (elementType == typeof(short)) return VarType.Int;      // signed 16-bit
        if (elementType == typeof(ushort)) return VarType.Word;    // unsigned 16-bit
        if (elementType == typeof(int)) return VarType.DInt;       // signed 32-bit
        if (elementType == typeof(uint)) return VarType.DWord;     // unsigned 32-bit
        if (elementType == typeof(float)) return VarType.Real;
        if (elementType == typeof(double)) return VarType.LReal;
        if (elementType == typeof(string)) return VarType.String;
        if (elementType == typeof(DateTime)) return VarType.DateTimeLong;

        // Deliberately unmapped: long/ulong (S7 LInt needs an explicit VarType), sbyte, decimal,
        // char, and any user type bridged by a ValueConverter.
        return null;
    }

    private static Type ElementTypeOf(Type clrType)
    {
        if (clrType == typeof(string)) return clrType;

        var enumerable = clrType.GetInterfaces().Append(clrType)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0] ?? clrType;
    }

    public IPropertyBuilder<TProperty> Property<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        var propertyInfo = (PropertyInfo)((MemberExpression)expression.Body).Member;
        if (propertyInfo == null)
            throw new ArgumentException($"Expression '{expression}' refers to a method, not a property.");

        if (!propertyDescriptors.ContainsKey(propertyInfo))
        {
            var inferred = FromClrType(propertyInfo.PropertyType);

            propertyDescriptors.Add(propertyInfo, new EntityPropertyDescriptor()
            {
                DeclaringType = typeof(T),
                PropertyName = propertyInfo.Name,
                VarTypeResolved = inferred is not null,
                DataItem = new DataItem()
                {
                    VarType = inferred ?? default,
                }
            });
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

public interface IPropertyDetailsBuilder<T> : IPropertyHasConversionBuilder<T>
{
    public IPropertyHasConversionBuilder<T> Having(int count);
    public IPropertyHasConversionBuilder<T> Having(int count, VarType varType);
    public IPropertyHasConversionBuilder<T> Having(int count, VarType varType, DataType dataType);
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
        // The address already states the width: DBX = Bit, DBB = Byte, DBW = Word, DBD = DWord.
        // Previously the CLR-inferred VarType was written back over it, so an address of
        // "DB200.DBW134" on a uint property silently read four bytes instead of two.
        // The address is the authoritative declaration of the PLC's layout; keep it.
        descriptor.DataItem = DataItem.FromAddress(address);
        descriptor.VarTypeResolved = true;

        return new PropertyDetailsBuilder<T>(descriptor);
    }
}

internal class PropertyCoordinatesBuilder<T>(EntityPropertyDescriptor descriptor) : IPropertyCoordinatesBuilder<T>
{
    public IPropertyDetailsBuilder<T> WithCoordinates(int startByte, byte bitAddress)
    {
        descriptor.DataItem.StartByteAdr = startByte;
        descriptor.DataItem.BitAdr = bitAddress;

        return new PropertyDetailsBuilder<T>(descriptor);
    }
}

internal class PropertyDetailsBuilder<T>(EntityPropertyDescriptor descriptor) : IPropertyDetailsBuilder<T>
{
    public IPropertyHasConversionBuilder<T> Having(int count)
    {
        descriptor.DataItem.Count = count;

        return new PropertyHasConversionBuilder<T>(descriptor);
    }

    public IPropertyHasConversionBuilder<T> Having(int count, VarType varType)
    {
        descriptor.DataItem.VarType = varType;
        descriptor.VarTypeResolved = true;

        return this.Having(count);
    }

    public IPropertyHasConversionBuilder<T> Having(int count, VarType varType, DataType dataType)
    {
        descriptor.DataItem.DataType = dataType;

        return this.Having(count, varType);
    }

    public void HasConversion(ValueConverter converter)
    {
        descriptor.ValueConverter = converter;
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
    public required DataItem DataItem { get; set; }
    public ValueConverter? ValueConverter { get; set; }

    /// <summary>
    /// True once the S7 type is known from the CLR type, the PLC address, or an explicit
    /// Having(count, varType). A descriptor still false at the end of model building would
    /// previously have silently defaulted to VarType.DWord.
    /// </summary>
    public bool VarTypeResolved { get; set; }

    public Type DeclaringType { get; init; } = typeof(object);
    public string PropertyName { get; init; } = "";
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
            EntityModels.Add(typeof(T), new Dictionary<PropertyInfo, EntityPropertyDescriptor>());
        }

        return new EntityTypeBuilder<T>(EntityModels[typeof(T)]);
    }

    /// <summary>
    /// Fails the model build for any property whose S7 type could not be determined. Reading the
    /// wrong width from a data block returns plausible-looking numbers, so this must be an error
    /// at startup rather than a default applied at runtime.
    /// </summary>
    internal void Validate()
    {
        var unresolved = EntityModels.Values
            .SelectMany(properties => properties.Values)
            .Where(descriptor => !descriptor.VarTypeResolved)
            .Select(descriptor => $"{descriptor.DeclaringType.Name}.{descriptor.PropertyName}")
            .ToList();

        if (unresolved.Count == 0) return;

        throw new InvalidOperationException(
            $"No S7 VarType could be determined for: {string.Join(", ", unresolved)}. " +
            "Pin it with a typed PLC address (for example \"DB1.DBW10\") or with " +
            "Having(count, varType).");
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