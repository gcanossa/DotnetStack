using System.Net;
using System.Text.RegularExpressions;
using S7.Net;

namespace GKit.PLC;

//CpuType cpu, string ip, int port, Int16 rack, Int16 slo
public interface IPlcContextOptions
{
    public CpuType CpuType { get; set; }
    public IPAddress Address { get; set; }
    public short Port { get; set; }
    public short Rack { get; set; }
    public short Slot { get; set; }
}

internal class PlcContextOptions : IPlcContextOptions
{
    public CpuType CpuType { get; set; } = CpuType.S7300;
    public IPAddress Address { get; set; } = IPAddress.Loopback;
    public short Port { get; set; } = 102;
    public short Rack { get; set; } = 0;
    public short Slot { get; set; } = 1;

    public static void ApplyConnectionString(IPlcContextOptions options, string connectionString)
    {
        var match = Regex.Match(connectionString, @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})(:(\d{1,5}))?(,\s*(\d+)(,\s*(\d+))?)?$");
        if (!match.Success)
            throw new ArgumentException($"Invalid connectionString: {connectionString}");
        
        if (match.Groups[1].Success)
            options.Address = IPAddress.Parse(match.Groups[1].Value);

        if (match.Groups[3].Success)
            options.Port = short.Parse(match.Groups[3].Value);

        if (match.Groups[5].Success)
            options.Rack = short.Parse(match.Groups[5].Value);

        if (match.Groups[7].Success)
            options.Slot = short.Parse(match.Groups[7].Value);
    }
    
    public static IPlcContextOptions FromConnectionString(string connectionString)
    {
        var options = new PlcContextOptions();
        
        ApplyConnectionString(options, connectionString);

        return options;
    }
}

public interface IPlcContextOptions<T> : IPlcContextOptions where T : PlcContext
{
}

internal class PlcContextOptions<T> : PlcContextOptions, IPlcContextOptions<T> where T : PlcContext
{
}