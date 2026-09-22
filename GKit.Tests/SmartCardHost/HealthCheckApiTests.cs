using GKit.SmartCardHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GKit.Tests.SmartCardHost;

/// <summary>
/// <c>AddSmartCardHostCheck</c> shipped in GKit.SmartCardHost 0.0.9 and is called by
/// StuffHR (<c>Program.cs</c>), but went missing from the dev line: it was added on main on top
/// of the pre-broker shape of the package and was never carried across the broker refactor.
/// <para>
/// These are compile-time guards as much as tests — deleting the API again breaks this build.
/// </para>
/// </summary>
public class HealthCheckApiTests
{
  [Fact]
  public void AddSmartCardHostCheck_registers_a_named_health_check()
  {
    var services = new ServiceCollection();

    services.AddHealthChecks().AddSmartCardHostCheck("SMART_CARD");

    var registration = services.BuildServiceProvider()
      .GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
      .Value.Registrations.Single();

    Assert.Equal("SMART_CARD", registration.Name);
  }

  [Fact]
  public async Task The_check_is_unhealthy_while_no_reader_is_attached()
  {
    // The broker only touches its hub context when broadcasting; Readers starts empty.
    var broker = new SmartCardStateBroker(null!);

    var result = await new SmartCardHostHealthCheck(broker)
      .CheckHealthAsync(new HealthCheckContext());

    Assert.Equal(HealthStatus.Unhealthy, result.Status);
  }
}
