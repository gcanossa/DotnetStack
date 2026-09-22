using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GKit.Authentication.Simple;

public static class SimpleExtensions
{
  /// <summary>
  /// Registers the <see cref="SimpleAccountManager{TContext,TUser}"/> and the cookie authentication
  /// handler it signs in with. The password hasher defaults to <see cref="Pbkdf2PasswordHasher"/>:
  /// register an <see cref="IPasswordHasher"/> before this call to override it.
  /// </summary>
  public static IServiceCollection AddSimpleAuthentication<TContext, TUser>(
    this IServiceCollection ext,
    Action<CookieAuthenticationOptions>? cookieConfig = null,
    Action<SimpleAccountManagerOptions<TUser>>? simpleConfig = null)
    where TContext : DbContext
    where TUser : class, ISimpleAccountUser
  {
    // Bind first, then apply the delegate: PostConfigure would run after configuration binding
    // and silently override appsettings.json, the same inversion fixed in ADExtensions.
    var options = ext.AddOptions<SimpleAccountManagerOptions<TUser>>()
      .BindConfiguration("GKit:SimpleAuthentication");

    if (simpleConfig is not null) options.Configure(simpleConfig);

    options
      .Validate(o => !string.IsNullOrWhiteSpace(o.AuthenticationType),
        "SimpleAuthentication AuthenticationType must not be empty")
      .ValidateOnStart();

    ext.AddHttpContextAccessor();
    ext.TryAddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
    ext.AddScoped<SimpleAccountManager<TContext, TUser>>();
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
