using GKit.Authentication.Blazor;

namespace GKit.Tests.Authentication;

public class LocalUrlTests
{
  [Theory]
  [InlineData("/")]
  [InlineData("/home")]
  [InlineData("/account/profile?tab=1")]
  [InlineData("/a/b#fragment")]
  [InlineData("~/home")]
  public void Same_application_paths_are_local(string url)
  {
    Assert.True(LocalUrl.IsLocal(url));
    Assert.Equal(url, LocalUrl.EnsureLocal(url));
  }

  [Theory]
  [InlineData("https://evil.example/steal")]
  [InlineData("http://evil.example")]
  [InlineData("//evil.example")]            // protocol-relative
  [InlineData("/\\evil.example")]           // backslash variant browsers normalise to //
  [InlineData("~//evil.example")]
  [InlineData("~/\\evil.example")]
  [InlineData("javascript:alert(1)")]
  [InlineData("home")]                      // no leading slash: ambiguous, treat as unsafe
  [InlineData("")]
  [InlineData(null)]
  public void Off_application_targets_are_rejected(string? url)
  {
    Assert.False(LocalUrl.IsLocal(url));
    Assert.Equal("/", LocalUrl.EnsureLocal(url));
  }

  [Fact]
  public void The_fallback_is_configurable()
  {
    Assert.Equal("/dashboard", LocalUrl.EnsureLocal("https://evil.example", "/dashboard"));
  }
}
