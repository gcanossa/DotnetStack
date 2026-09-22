using GKit.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GKIT-NAMESPACE;

/// <summary>
/// Register in Program.cs:
/// <code>builder.Services.AddSingleton&lt;ICommandLineRunner, SampleCommandLineRunner&gt;();</code>
/// Application.Wrap runs every matching runner, then starts the host unless one of them asked it
/// not to - which is why a command that should not also serve HTTP returns true below.
/// </summary>
public class SampleCommandLineRunner : CommandLineRunnerBase
{
  public override bool Matches(IHost host, string[] args) => args.Contains("GKIT-FLAG");

  public override bool ShouldDisableHostRun(IHost host, string[] args) => true;

  public override string Help => "GKIT-FLAG  describe what this command does";

  public override async Task Execute(IHost host, string[] args)
  {
    // The base pauses the schedulers first, so jobs do not fire underneath the command.
    await base.Execute(host, args);

    using var scope = host.Services.CreateScope();

    Console.WriteLine("SampleCommandLineRunner running");
  }
}
