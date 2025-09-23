using Microsoft.Extensions.Hosting;

namespace GKit.Application;

public interface ICommandLineRunner
{
    public bool ShouldDisableHostRun(IHost host, string[] args);
    public bool Matches(IHost host, string[] args);
    public Task Execute(IHost host, string[] args);
}