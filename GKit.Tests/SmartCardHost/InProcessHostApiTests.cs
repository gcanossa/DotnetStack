using GKit.SmartCardHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GKit.Tests.SmartCardHost;

/// <summary>
/// SmartCardState shipped as the local-events API, was deleted by the SignalR broker refactor
/// (28039b1) and is restored here as the in-process alternative to the broker, selected via
/// AddGKitSmartCardHostInProcess instead of AddGKitSmartCardHost.
/// <para>
/// These are compile-time guards as much as tests — deleting the API again breaks this build.
/// </para>
/// </summary>
public class InProcessHostApiTests
{
  [Fact]
  public void AddGKitSmartCardHostInProcess_registers_SmartCardState_without_SignalR()
  {
    var services = new ServiceCollection();

    services.AddLogging();
    services.AddGKitSmartCardHostInProcess();

    var provider = services.BuildServiceProvider();

    var state = provider.GetRequiredService<SmartCardState>();
    Assert.Same(state, provider.GetRequiredService<ISmartCardStateSink>());
    Assert.Contains(provider.GetServices<IHostedService>(), s => s is SmartCardManager);
  }

  [Fact]
  public async Task AddSmartCardHostCheck_reports_unhealthy_against_SmartCardState_while_no_reader_is_attached()
  {
    var services = new ServiceCollection();

    services.AddLogging();
    services.AddGKitSmartCardHostInProcess();
    services.AddHealthChecks().AddSmartCardHostCheck("SMART_CARD");

    var provider = services.BuildServiceProvider();
    var registration = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
      .Value.Registrations.Single(r => r.Name == "SMART_CARD");

    var result = await registration.Factory(provider).CheckHealthAsync(new HealthCheckContext());

    Assert.Equal(HealthStatus.Unhealthy, result.Status);
  }

  [Fact]
  public async Task SmartCardState_raises_its_events_and_tracks_the_last_card()
  {
    var state = new SmartCardState();
    ISmartCardStateSink sink = state;

    string[]? raisedReaders = null;
    state.ReadersChanged += readers => raisedReaders = readers;
    string? raisedCard = null;
    state.CardAvailable += cardId => raisedCard = cardId;

    await sink.OnReadersChanged(["ACR122U"]);
    await sink.OnCardAvailable("04A224B2");

    Assert.Equal(["ACR122U"], state.Readers);
    Assert.Equal(["ACR122U"], raisedReaders ?? []);
    Assert.Equal("04A224B2", state.LastAvailableCardId);
    Assert.Equal("04A224B2", raisedCard);
    Assert.NotNull(state.LastAvailableCardAt);
  }
}
