using GKit.OpcUa;
using Opc.Ua;
using Opc.Ua.Client;

namespace GKit.Tests.OpcUa;

/// <summary>
/// The session pool's bookkeeping — which is where the leak and the churn lived — is decidable
/// without a server. Whether a session then successfully opens is not, and is covered by the
/// manual checklist in <c>GKIT-TEST-PLAN.md</c>.
/// </summary>
public class ConnectionPoolTests : IDisposable
{
  private sealed class FakeOptions : IOpcUaContextOptions
  {
    public string? ServerUrl { get; set; } = "opc.tcp://localhost:4840";
    public ReverseConnectManager? ReverseConnectManager => null;
    public CertificateValidator? CertificateValidator => null;
    public IUserIdentity? UserIdentity => null;
    public ApplicationConfiguration? ApplicationConfiguration => null;
    public ITelemetryContext TelemetryContext { get; set; } = new InjectableTelemetryContext(
      Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { }));
    public bool AcceptUntrustedCertificates => true;
    public TimeSpan KeepAliveInterval => TimeSpan.FromSeconds(5);
    public TimeSpan ReconnectPeriod => TimeSpan.FromSeconds(1);
    public TimeSpan ReconnectPeriodExponentialBackoff => TimeSpan.FromSeconds(15);
    public TimeSpan SessionLifeTime => TimeSpan.FromMinutes(1);
  }

  private readonly List<IOpcUaContextOptions> _created = [];

  private FakeOptions NewOptions()
  {
    var options = new FakeOptions();
    _created.Add(options);
    return options;
  }

  public void Dispose()
  {
    foreach (var options in _created)
      OpcUaConnectionPool.Remove(options)?.Dispose();
  }

  [Fact]
  public void The_same_options_always_yield_the_same_connection()
  {
    // This is the whole point of the pool: a session is expensive and servers cap how many
    // they will hold, so every context built from one options instance must share one session.
    var options = NewOptions();

    var first = OpcUaConnectionPool.GetOrCreate(options);
    var second = OpcUaConnectionPool.GetOrCreate(options);

    Assert.Same(first, second);
  }

  [Fact]
  public void Distinct_options_get_distinct_connections()
  {
    Assert.NotSame(
      OpcUaConnectionPool.GetOrCreate(NewOptions()),
      OpcUaConnectionPool.GetOrCreate(NewOptions()));
  }

  [Fact]
  public void A_concurrent_race_creates_exactly_one_connection()
  {
    // TryAdd was used before: the losing thread's freshly constructed OpcUaConnection was
    // dropped on the floor undisposed and never connected.
    var options = NewOptions();

    var connections = Enumerable.Range(0, 64)
      .AsParallel()
      .Select(_ => OpcUaConnectionPool.GetOrCreate(options))
      .Distinct()
      .ToList();

    Assert.Single(connections);
  }

  [Fact]
  public void Removing_takes_the_connection_out_of_the_pool()
  {
    var options = NewOptions();
    var connection = OpcUaConnectionPool.GetOrCreate(options);

    var removed = OpcUaConnectionPool.Remove(options);

    Assert.Same(connection, removed);
    Assert.False(OpcUaConnectionPool.Connections.ContainsKey(options));
  }

  [Fact]
  public void Removing_something_absent_is_not_an_error()
  {
    Assert.Null(OpcUaConnectionPool.Remove(NewOptions()));
  }

  [Fact]
  public void A_connection_created_after_removal_is_a_new_one()
  {
    var options = NewOptions();

    var first = OpcUaConnectionPool.GetOrCreate(options);
    OpcUaConnectionPool.Remove(options)?.Dispose();
    var second = OpcUaConnectionPool.GetOrCreate(options);

    Assert.NotSame(first, second);
  }
}
