using Microsoft.AspNetCore.Components;

namespace GKit.Authentication.Blazor;

/// <summary>
/// Guards against open redirects through a <c>returnUrl</c> query parameter.
/// <para>
/// A login page that navigates to an attacker-supplied absolute URL hands the freshly
/// authenticated user to another site — a classic phishing primitive, and more damaging here
/// because it happens immediately after a successful sign-in.
/// </para>
/// </summary>
public static class LocalUrl
{
  /// <summary>
  /// Returns <paramref name="url"/> when it is a safe same-application path, otherwise
  /// <paramref name="fallback"/>. Mirrors the checks in ASP.NET Core's <c>Url.IsLocalUrl</c>.
  /// </summary>
  public static string EnsureLocal(string? url, string fallback = "/")
  {
    return IsLocal(url) ? url! : fallback;
  }

  public static bool IsLocal(string? url)
  {
    if (string.IsNullOrEmpty(url)) return false;

    // "/foo" is local; "//evil.com" and "/\evil.com" are protocol-relative and are not.
    if (url[0] == '/')
      return url.Length == 1 || (url[1] != '/' && url[1] != '\\');

    // "~/foo" is local; "~//evil.com" and "~/\evil.com" are not.
    if (url.Length > 1 && url[0] == '~' && url[1] == '/')
      return url.Length == 2 || (url[2] != '/' && url[2] != '\\');

    // Anything carrying a scheme or authority is off-application.
    return false;
  }

  /// <summary>
  /// Builds a <c>returnUrl</c> value for the current page that will survive
  /// <see cref="EnsureLocal"/> — <see cref="NavigationManager.Uri"/> is absolute and would not.
  /// </summary>
  public static string CurrentPath(NavigationManager navigationManager)
  {
    ArgumentNullException.ThrowIfNull(navigationManager);

    return "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);
  }
}
