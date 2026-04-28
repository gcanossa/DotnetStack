using System.Net;
using S7.Net;

namespace GKit.PLC;

public interface IPlcContextOptionsSpecBuilder<T> where T : PlcContext
{
    public IPlcContextOptionsSpecBuilder<T> Address(IPAddress address);
    public IPlcContextOptionsSpecBuilder<T> Port(short port);
    public IPlcContextOptionsSpecBuilder<T> Rack(short rack);
    public IPlcContextOptionsSpecBuilder<T> Slot(short slot);

    public Task<IPlcContextOptions<T>> BuildAsync();
}

internal class PlcContextOptionsSpecBuilder<T>(PlcContextOptionsBuilder<T> builder)
    : IPlcContextOptionsSpecBuilder<T> where T : PlcContext
{
    public IPlcContextOptionsSpecBuilder<T> Address(IPAddress address)
    {
        builder.Options.Address = address;
        return this;
    }

    public IPlcContextOptionsSpecBuilder<T> Port(short port)
    {
        builder.Options.Port = port;
        return this;
    }

    public IPlcContextOptionsSpecBuilder<T> Rack(short rack)
    {
        builder.Options.Rack = rack;
        return this;
    }

    public IPlcContextOptionsSpecBuilder<T> Slot(short slot)
    {
        builder.Options.Slot = slot;
        return this;
    }

    public Task<IPlcContextOptions<T>> BuildAsync()
    {
        return Task.FromResult<IPlcContextOptions<T>>(builder.Options);
    }
}

public sealed class PlcContextOptionsBuilder<T> where T : PlcContext
{
    internal PlcContextOptions<T> Options { get; }
    internal IServiceProvider ServiceProvider { get; }

    internal PlcContextOptionsBuilder(PlcContextOptions<T> options, IServiceProvider provider)
    {
        Options = options;
        ServiceProvider = provider;
    }

    public IPlcContextOptionsSpecBuilder<T> UseCpu(
        CpuType cpuType, string? connectionString)
    {
        Options.CpuType = cpuType;

        if (connectionString is not null)
        {
            PlcContextOptions.ApplyConnectionString(Options, connectionString);
        }

        return new PlcContextOptionsSpecBuilder<T>(this);
    }
}