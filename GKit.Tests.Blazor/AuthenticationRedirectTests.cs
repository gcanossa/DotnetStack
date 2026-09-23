using Bunit;
using Bunit.TestDoubles;
using GKit.Authentication.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.Tests.Blazor;

public class AuthenticationRedirectTests : TestContext
{
  /// <summary>Concrete login page over the abstract base, so the flow can be driven.</summary>
  private sealed class TestLogin : LoginComponentBase<TestLogin.Credentials>
  {
    public sealed class Credentials { public string User { get; set; } = ""; }

    public bool Succeed { get; set; } = true;

    protected override Task<bool> SignInAsync(Credentials credentials) => Task.FromResult(Succeed);

    public Task Login() => LoginUser();
  }

  private sealed class TestLogout : LogoutComponentBase
  {
    protected override Task SignOutAsync() => Task.CompletedTask;
  }

  private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

  /// <summary>
  /// ReturnUrl is a [SupplyParameterFromQuery] parameter, so it can only be provided through
  /// the URL — which is exactly how an attacker would supply it.
  /// </summary>
  private void GivenReturnUrl(string returnUrl) =>
    Nav.NavigateTo(Nav.GetUriWithQueryParameter("ReturnUrl", returnUrl));

  [Theory]
  [InlineData("https://evil.example/steal")]
  [InlineData("//evil.example")]
  [InlineData("/\\evil.example")]
  [InlineData("javascript:alert(1)")]
  public void Login_refuses_to_redirect_off_application(string returnUrl)
  {
    // The user has just authenticated; handing them to an attacker's site here is the most
    // damaging moment to do it.
    GivenReturnUrl(returnUrl);
    var landed = Nav.Uri;

    var component = Render<TestLogin>();
    component.Instance.Login().GetAwaiter().GetResult();

    // Fell back to "/" rather than following the supplied target.
    Assert.Equal(Nav.BaseUri, Nav.Uri);
    Assert.NotEqual(landed, Nav.Uri);
  }

  [Fact]
  public void Login_honours_a_same_application_return_url()
  {
    GivenReturnUrl("/orders/42");

    var component = Render<TestLogin>();
    component.Instance.Login().GetAwaiter().GetResult();

    Assert.Equal($"{Nav.BaseUri}orders/42", Nav.Uri);
  }

  [Fact]
  public void A_failed_login_does_not_navigate()
  {
    GivenReturnUrl("/orders/42");
    var landed = Nav.Uri;

    var component = Render<TestLogin>();
    component.Instance.Succeed = false;

    component.Instance.Login().GetAwaiter().GetResult();

    Assert.Equal(landed, Nav.Uri);
    Assert.True(component.Instance.FailedLoginAttempt);
  }

  [Fact]
  public void Logout_refuses_to_redirect_off_application()
  {
    GivenReturnUrl("https://evil.example");

    Render<TestLogout>();

    Assert.Equal(Nav.BaseUri, Nav.Uri);
  }

  [Fact]
  public void RedirectToLogin_sends_a_relative_return_url()
  {
    // It used to pass NavigationManager.Uri, which is absolute — the new guard would reject it
    // and drop the user on "/" after logging in instead of back where they were.
    Nav.NavigateTo("/orders/42");

    Render<RedirectToLogin>();

    Assert.StartsWith($"{Nav.BaseUri}account/login?returnUrl=", Nav.Uri);
    Assert.Contains("returnUrl=%2Forders%2F42", Nav.Uri);
    Assert.DoesNotContain("http%3A", Nav.Uri);
  }

  [Fact]
  public void RedirectToLogin_uses_the_cascaded_paths()
  {
    var options = new AuthenticationOptions { LoginPath = "/signin" };

    Render<RedirectToLogin>(p => p.AddCascadingValue(options));

    Assert.StartsWith($"{Nav.BaseUri}signin?returnUrl=", Nav.Uri);
  }

  [Fact]
  public void RedirectToAccessDenied_sends_a_relative_return_url()
  {
    Nav.NavigateTo("/admin");

    Render<RedirectToAccessDenied>();

    Assert.StartsWith($"{Nav.BaseUri}account/access-denied?returnUrl=", Nav.Uri);
    Assert.Contains("returnUrl=%2Fadmin", Nav.Uri);
  }
}
