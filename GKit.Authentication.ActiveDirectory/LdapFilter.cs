using System.Text;

namespace GKit.Authentication.ActiveDirectory;

/// <summary>
/// RFC 4515 escaping for values interpolated into an LDAP search filter.
/// <para>
/// The username was previously interpolated raw, so a value such as <c>*)(objectClass=*</c>
/// changed the filter's meaning. Exploitability was limited — the bind has already succeeded
/// with that username — but a search filter must never be assembled from unescaped input.
/// </para>
/// </summary>
public static class LdapFilter
{
  /// <summary>Escapes <paramref name="value"/> for use as an assertion value in a filter.</summary>
  public static string Escape(string? value)
  {
    if (string.IsNullOrEmpty(value)) return string.Empty;

    var builder = new StringBuilder(value.Length);

    foreach (var c in value)
    {
      switch (c)
      {
        case '\\': builder.Append("\\5c"); break;
        case '*': builder.Append("\\2a"); break;
        case '(': builder.Append("\\28"); break;
        case ')': builder.Append("\\29"); break;
        case '\0': builder.Append("\\00"); break;
        case '/': builder.Append("\\2f"); break;
        default: builder.Append(c); break;
      }
    }

    return builder.ToString();
  }
}
