using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.DirectoryServices.Protocols;
using System.Security.Claims;
using System.Text;

namespace GC.Authentication.ActiveDirectory;

public class ADAccountManager(
  IHttpContextAccessor httpContextAccessor,
  IOptions<ADAccountManagerOptions> options,
  ILogger<ADAccountManager> logger)
{
  private readonly ILogger<ADAccountManager> _logger = logger;
  private readonly IOptions<ADAccountManagerOptions> _options = options;
  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

  /// <summary>
  /// Tries to binnd to an AD controller using a given username and password.
  /// </summary>
  /// <param name="username"></param>
  /// <param name="password"></param>
  /// <returns>LdapConncetion on success. Throws an exception otherwise.</returns>
  protected LdapConnection Connect(string username, string password)
  {
    try
    {
      username = OperatingSystem.IsWindows() ? username : $"{_options.Value.Domain}\\{username}";
      var authType = OperatingSystem.IsWindows() ? AuthType.Negotiate : AuthType.Basic;

      var connection = new LdapConnection(new(_options.Value.Host, _options.Value.Port), new(username, password), authType);

      connection.SessionOptions.ProtocolVersion = 3;

      connection.SessionOptions.SecureSocketLayer = _options.Value.IsSecure;

      connection.Bind();

      return connection;
    }
    catch (Exception e)
    {
      _logger.LogWarning(e, "Unable to connect to the LDAP server");

      throw;
    }
  }
  
  /// <summary>
  /// Extract an attribute fro an LDAP search result.
  /// </summary>
  /// <param name="entry"></param>
  /// <param name="name"></param>
  /// <returns></returns>
  protected DirectoryAttribute? GetAttribute(SearchResultAttributeCollection entry, string name)
  {
    return entry.Contains(name) ? entry[name] : null;
  }

  /// <summary>
  /// Tries to retrieve the user information from Active Dricetory given its username and password.
  /// </summary>
  /// <param name="username"></param>
  /// <param name="password"></param>
  /// <param name="user"></param>
  /// <returns>True on success, setting the user out variable.</returns>
  public bool TryGetUserInfo(string username, string password, out ADUser? user)
  {
    user = null;

    try
    {
      using var connection = Connect(username, password);

      var query = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={username})(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";
      var request = new SearchRequest(
        _options.Value.QueryBase,
        query,
        SearchScope.Subtree,
        [
          "objectGUID",
          "sAMAccountName",
          "displayName",
          "mail",
          "memberOf"
        ]);

      var response = (SearchResponse)connection.SendRequest(request);

      var results = response.Entries.Cast<SearchResultEntry>();

      if (!results.Any())
        return false;

      var resultsEntry = results.First();
      user = new ADUser(
        new Guid((GetAttribute(resultsEntry.Attributes, "objectGUID")?[0] as byte[])!),
        GetAttribute(resultsEntry.Attributes, "sAMAccountName")?[0].ToString()!,
        GetAttribute(resultsEntry.Attributes, "displayName")?[0].ToString()!,
        GetAttribute(resultsEntry.Attributes, "mail")?[0].ToString()!,
        GetAttribute(resultsEntry.Attributes, "memberOf")?.GetValues(typeof(byte[]))
          .Select(p => Encoding.Default.GetString((byte[])p).ToLower())
          .ToArray() ?? []
      );

      return true;
    }
    catch(Exception e)
    {
      _logger.LogWarning(e, "Unable to query the LDAP server");
      return false;
    }
  }

  /// <summary>
  /// Tries to sign a user in with the Active Directory credentials. 
  /// On success a ClaimsPrincipal is created and used to sign in with cookie autentication.
  /// </summary>
  /// <param name="username"></param>
  /// <param name="password"></param>
  /// <returns></returns>
  public async Task<bool> SignInAsync(string username, string password)
  {
    if (!TryGetUserInfo(username, password, out var user))
      return false;

    Claim[] claims = [
      new (ClaimTypes.NameIdentifier, user!.Id.ToString()),
      new (ClaimTypes.WindowsAccountName, user.AccountName),
      new (ClaimTypes.Name, user.DisplayName),
      new (ClaimTypes.Email, user.Mail ?? ""),
      ..user.GroupsNames.Select(p => new Claim(ClaimTypes.Role, p))];

    var identity = new ClaimsIdentity(
        claims,
        "LDAP", // what goes to User.Identity.AuthenticationType
        ClaimTypes.Name, // which claim is for storing user name in User.Identity.Name
        ClaimTypes.Role // which claim is for storing user roles, needed for User.IsInRole()
    );
    var principal = new ClaimsPrincipal(identity);

    if (_httpContextAccessor.HttpContext != null)
    {
      try
      {
        await _httpContextAccessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return true;
      }
      catch (Exception e)
      {
        _logger.LogInformation(e, "Signing in has failed for user: {User}", username);
      }
    }

    return false;
  }

  /// <summary>
  /// Signs out the current user.
  /// </summary>
  /// <returns></returns>
  /// <exception cref="Exception"></exception>
  public async Task SignOutAsync()
  {
    if (_httpContextAccessor.HttpContext != null)
    {
      await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
    else
    {
      throw new Exception("For some reasons, HTTP context is null, signing out cannot be performed");
    }
  }
}