using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GKit.Authentication.ActiveDirectory;

public static class ADExtensions
{
  public static IServiceCollection AddActiveDirectoryAuthentication(
    this IServiceCollection ext,
    Action<CookieAuthenticationOptions>? cookieConfig = null,
    Action<ADAccountManagerOptions>? adConfig = null)
  {
    // PostConfigure ran *after* configuration binding, so a caller-supplied delegate silently
    // overrode appsettings.json — backwards. Bind first, then apply the delegate, then validate.
    var options = ext.AddOptions<ADAccountManagerOptions>()
      .BindConfiguration("GKit:ActiveDirectory");

    if (adConfig is not null) options.Configure(adConfig);

    options
      .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "ActiveDirectory Host must be configured")
      .Validate(o => !string.IsNullOrWhiteSpace(o.QueryBase), "ActiveDirectory QueryBase must be configured")
      .ValidateOnStart();

    ext.AddHttpContextAccessor();
    ext.AddScoped<ADAccountManager>();
    ext.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
      .AddCookie(options =>
      {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/access-denied";

        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        cookieConfig?.Invoke(options);
      });
    return ext;
  }
}