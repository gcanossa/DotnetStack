using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GKit.RENTRI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.Tests.RENTRI;

public class RentriEnvironmentTests
{
  /// <summary>
  /// AddRentriServices binds RentriOptions from configuration (the same pattern GKit.Quartz
  /// uses), so IConfiguration must be present exactly as it is in a real host.
  /// </summary>
  private static IServiceCollection NewServices() =>
    new ServiceCollection()
      .AddLogging()
      .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

  private static ClientOptions NewOptions() => new()
  {
    Issuer = "issuer",
    Audience = "",
    BaseUrl = "",
    Certificate = SelfSigned()
  };

  private static X509Certificate2 SelfSigned()
  {
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=gkit-tests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
  }

  [Fact]
  public void AsDemo_points_at_the_demo_environment()
  {
    var options = NewOptions().AsDemo();

    Assert.Equal(RentriEndpoints.DemoBaseUrl, options.BaseUrl);
    Assert.Equal(RentriEndpoints.DemoAudience, options.Audience);
    Assert.True(options.IsDemo());
    Assert.False(options.IsLive());
  }

  [Fact]
  public void AsLive_points_at_the_live_environment()
  {
    var options = NewOptions().AsLive();

    Assert.True(options.IsLive());
    Assert.False(options.IsDemo());
  }

  [Fact]
  public void As_mutates_in_place_while_To_returns_a_copy()
  {
    var original = NewOptions().AsLive();

    var copy = original.ToDemo();

    Assert.True(copy.IsDemo());
    Assert.True(original.IsLive());   // To must not mutate its receiver
    Assert.NotSame(original, copy);

    original.AsDemo();
    Assert.True(original.IsDemo());
  }

  [Theory]
  [InlineData("https://api.rentri.gov.it/anagrafiche/v1.0", "https://demoapi.rentri.gov.it",
              "https://demoapi.rentri.gov.it/anagrafiche/v1.0")]
  [InlineData("https://api.rentri.gov.it", "https://demoapi.rentri.gov.it",
              "https://demoapi.rentri.gov.it")]
  [InlineData("https://api.rentri.gov.it/formulari/v1.0/", "https://demoapi.rentri.gov.it/",
              "https://demoapi.rentri.gov.it/formulari/v1.0")]
  public void Rebase_swaps_the_host_and_keeps_the_path(string generated, string target, string expected)
  {
    Assert.Equal(expected, RentriEndpoints.Rebase(generated, target));
  }

  [Fact]
  public void Rebase_leaves_the_url_alone_when_no_target_is_given()
  {
    const string generated = "https://api.rentri.gov.it/anagrafiche/v1.0";

    Assert.Equal(generated, RentriEndpoints.Rebase(generated, ""));
  }

  [Fact]
  public void An_anonymous_client_targets_the_configured_environment()
  {
    // The status probes use anonymous clients. Previously they were built with a null
    // ClientOptions, which skipped the base-URL rewrite entirely, so a demo-configured
    // application health-checked production.
    var provider = NewServices()
      .AddRentriServices(options => options.Environment = RentriEnvironment.Demo)
      .BuildServiceProvider();

    using var client = provider.GetRequiredService<AnagraficheClientFactory>().CreateAnonymousClient();

    Assert.StartsWith(RentriEndpoints.DemoBaseUrl, client.BaseUrl);
  }

  [Fact]
  public void An_authenticated_client_targets_its_options_environment()
  {
    var provider = NewServices()
      .AddRentriServices(options => options.Environment = RentriEnvironment.Live)
      .BuildServiceProvider();

    using var client = provider.GetRequiredService<AnagraficheClientFactory>()
      .CreateClient(NewOptions().AsDemo());

    Assert.StartsWith(RentriEndpoints.DemoBaseUrl, client.BaseUrl);
  }

  [Fact]
  public void The_http_client_comes_from_the_factory_pool()
  {
    // A dedicated HttpClient per call left a socket in TIME_WAIT each time; the named client
    // must be registered so handlers are pooled and rotated.
    var provider = NewServices()
      .AddRentriServices()
      .BuildServiceProvider();

    var factory = provider.GetRequiredService<IHttpClientFactory>();

    Assert.NotNull(factory.CreateClient(RentriHttpClientFactory.ClientName));
  }
}
