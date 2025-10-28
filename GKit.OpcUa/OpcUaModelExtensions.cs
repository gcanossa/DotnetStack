using Opc.Ua;

namespace GKit.OpcUa;

public static class OpcUaModelExtensions
{
    public static void Deconstruct<T1>(
        this object[] array, out T1 el0)
    {
        el0 = (T1)array[0];
    }
    
    public static void Deconstruct<T1, T2>(
        this object[] array, out T1 el0, out T2 el1)
    {
        Deconstruct(array, out el0);
        el1 = (T2)array[1];
    }
    
    public static void Deconstruct<T1, T2, T3>(
        this object[] array, out T1 el0, out T2 el1, out T3 el2)
    {
        Deconstruct(array, out el0, out el1);
        el2 = (T3)array[2];
    }
    
    public static void Deconstruct<T1, T2, T3, T4>(
        this object[] array, out T1 el0, out T2 el1, out T3 el2, out T4 el3)
    {
        Deconstruct(array, out el0, out el1, out el2);
        el3 = (T4)array[3];
    }
    
    public static void Deconstruct<T1, T2, T3, T4, T5>(
        this object[] array, out T1 el0, out T2 el1, out T3 el2, out T4 el3, out T5 el4)
    {
        Deconstruct(array, out el0, out el1, out el2, out el3);
        el4 = (T5)array[4];
    }
    
    public static void ThrowIfError(this StatusCode value)
    {
        if (StatusCode.IsNotGood(value))
            throw new Exception($"Error transferring data: {value}");
    }

    public static void ThrowIfAnyError(this IEnumerable<StatusCode> values)
    {
        foreach (var item in values)
            item.ThrowIfError();
    }
    
    public static void ThrowIfError(this DataValue? value)
    {
        if (value == null)
            throw new Exception("No data read");

        value.StatusCode.ThrowIfError();
    }

    public static void ThrowIfAnyError(this IEnumerable<DataValue> values)
    {
        values.Select(p => p.StatusCode).ThrowIfAnyError();
    }

    public static object[] ReadData(this IEnumerable<DataValue> values)
    {
        var dataValues = values as DataValue[] ?? values.ToArray();
        
        dataValues.ThrowIfAnyError();
        
        return dataValues.Select(p => p.Value).ToArray();
    }
    
    
}