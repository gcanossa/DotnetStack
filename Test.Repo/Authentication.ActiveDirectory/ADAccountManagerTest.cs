using GC.Authentication.ActiveDirectory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;

namespace Test.Repo.Authentcation.ActiveDirectory;

public class ADAccountManagerTest
{
  private readonly ServiceProvider provider;

  public ADAccountManagerTest()
  {
    var services = new ServiceCollection();

    services.AddLogging();
    services.AddOptions();
    services.AddActiveDirectoryAuthentication(adConfig: p =>
      {
        p.Host = "192.168.11.50";
        p.Domain = "win.virtualbox.org";
        p.QueryBase = "CN=Users, DC=win, DC=virtualbox, DC=org";
      });

    provider = services.BuildServiceProvider();
  }

  [Fact]
  public void GetUserInfo()
  {
    using var scope = provider.CreateScope();

    var manager = scope.ServiceProvider.GetRequiredService<ADAccountManager>();

    var result = manager.TryGetUserInfo("user", "user", out var user);
    Assert.True(result);
  }
}