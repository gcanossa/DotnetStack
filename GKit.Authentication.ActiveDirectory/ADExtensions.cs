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
    if (adConfig is not null) ext.PostConfigure(adConfig);

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