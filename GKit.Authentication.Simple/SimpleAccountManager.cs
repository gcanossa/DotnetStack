using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GKit.Authentication.Simple;

/// <summary>
/// Signs users in against an application owned EntityFramework entity, using cookie authentication.
/// Mirrors the API of GKit.Authentication.ActiveDirectory's ADAccountManager, so that the two
/// can be swapped with minimal changes.
/// </summary>
public class SimpleAccountManager<TContext, TUser>(
  IHttpContextAccessor httpContextAccessor,
  IDbContextFactory<TContext> dbContextFactory,
  IPasswordHasher passwordHasher,
  IOptions<SimpleAccountManagerOptions<TUser>> options,
  ILogger<SimpleAccountManager<TContext, TUser>> logger)
  where TContext : DbContext
  where TUser : class, ISimpleAccountUser
{
  protected SimpleAccountManagerOptions<TUser> Options => options.Value;

  /// <summary>
  /// Produces the value to store in <see cref="ISimpleAccountUser.PasswordHash"/>.
  /// </summary>
  public string ComputePasswordHash(string password) => passwordHasher.Hash(password);

  private IQueryable<TUser> SignInSet(TContext ctx)
  {
    var set = ctx.Set<TUser>().AsQueryable();
    return Options.SignInQuery?.Invoke(set) ?? set;
  }

  private IQueryable<TUser> LookupSet(TContext ctx)
  {
    var set = ctx.Set<TUser>().AsQueryable();
    return Options.LookupQuery?.Invoke(set) ?? set;
  }

  /// <summary>
  /// Retrieves the user matching the given credentials, or null when the credentials are invalid,
  /// the account is filtered out by <see cref="SimpleAccountManagerOptions{TUser}.SignInQuery"/>
  /// or the store is unreachable.
  /// </summary>
  public async Task<TUser?> FindUserAsync(string username, string password, CancellationToken ct = default)
  {
    try
    {
      await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

      var user = await SignInSet(ctx).FirstOrDefaultAsync(p => p.AccountName == username, ct);

      return user is not null && passwordHasher.Verify(password, user.PasswordHash) ? user : null;
    }
    catch (Exception e)
    {
      logger.LogWarning(e, "Unable to query database");
      return null;
    }
  }

  /// <summary>
  /// Blocking overload of <see cref="FindUserAsync"/>, for use in command line runners.
  /// </summary>
  public bool TryGetUserInfo(string username, string password, out TUser? user)
  {
    user = FindUserAsync(username, password).ConfigureAwait(false).GetAwaiter().GetResult();

    return user is not null;
  }

  /// <summary>
  /// Changes the password of a user, provided the current one matches.
  /// </summary>
  public async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword,
    CancellationToken ct = default)
  {
    try
    {
      await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

      var user = await LookupSet(ctx).FirstOrDefaultAsync(p => p.AccountName == username, ct);

      if (user is null || !passwordHasher.Verify(oldPassword, user.PasswordHash)) return false;

      user.PasswordHash = passwordHasher.Hash(newPassword);
      await ctx.SaveChangesAsync(ct);

      return true;
    }
    catch (Exception e)
    {
      logger.LogWarning(e, "Unable to query database");
      return false;
    }
  }

  /// <summary>
  /// Sets the password of a user without checking the current one.
  /// </summary>
  public async Task<bool> ResetPasswordAsync(string username, string password, CancellationToken ct = default)
  {
    try
    {
      await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

      var user = await LookupSet(ctx).FirstOrDefaultAsync(p => p.AccountName == username, ct);

      if (user is null) return false;

      user.PasswordHash = passwordHasher.Hash(password);
      await ctx.SaveChangesAsync(ct);

      return true;
    }
    catch (Exception e)
    {
      logger.LogWarning(e, "Unable to query database");
      return false;
    }
  }

  /// <summary>
  /// Stores a new account. The caller builds the entity, so that application specific
  /// properties can be set; <see cref="ISimpleAccountUser.PasswordHash"/> is overwritten.
  /// </summary>
  public async Task<bool> RegisterAsync(TUser user, string password, CancellationToken ct = default)
  {
    try
    {
      await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

      user.PasswordHash = passwordHasher.Hash(password);

      ctx.Set<TUser>().Add(user);
      await ctx.SaveChangesAsync(ct);

      return true;
    }
    catch (Exception e)
    {
      logger.LogWarning(e, "Unable to query database");
      return false;
    }
  }

  /// <summary>
  /// Builds the principal signed in by <see cref="SignInAsync"/>. Override to take full control
  /// over the emitted claims.
  /// </summary>
  protected virtual ClaimsPrincipal CreatePrincipal(TUser user)
  {
    Claim[] claims =
    [
      new(ClaimTypes.NameIdentifier, user.Identifier),
      new(ClaimTypes.Name, user.DisplayName ?? user.AccountName),
      new(ClaimTypes.Email, user.Mail ?? ""),
      ..user.Roles.Select(p => new Claim(ClaimTypes.Role, p))
    ];

    var identity = new ClaimsIdentity(
      claims,
      Options.AuthenticationType, // what goes to User.Identity.AuthenticationType
      ClaimTypes.Name, // which claim is for storing user name in User.Identity.Name
      ClaimTypes.Role // which claim is for storing user roles, needed for User.IsInRole()
    );

    Options.EnrichClaims?.Invoke(identity, user);

    return new ClaimsPrincipal(identity);
  }

  /// <summary>
  /// Tries to sign a user in with the stored credentials.
  /// On success a ClaimsPrincipal is created and used to sign in with cookie authentication.
  /// </summary>
  public async Task<bool> SignInAsync(string username, string password, CancellationToken ct = default)
  {
    var user = await FindUserAsync(username, password, ct);

    if (user is null)
      return false;

    if (httpContextAccessor.HttpContext != null)
    {
      try
      {
        await httpContextAccessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
          CreatePrincipal(user));
        return true;
      }
      catch (Exception e)
      {
        logger.LogInformation(e, "Signing in has failed for user: {User}", username);
      }
    }

    return false;
  }

  /// <summary>
  /// Signs out the current user.
  /// </summary>
  public async Task SignOutAsync()
  {
    if (httpContextAccessor.HttpContext != null)
    {
      await httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
    else
    {
      throw new Exception("For some reasons, HTTP context is null, signing out cannot be performed");
    }
  }
}
