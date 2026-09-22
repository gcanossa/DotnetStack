using System.Security.Claims;

namespace GKit.Authentication.Simple;

public class SimpleAccountManagerOptions<TUser>
  where TUser : class, ISimpleAccountUser
{
  /// <summary>
  /// Value reported by User.Identity.AuthenticationType.
  /// </summary>
  public string AuthenticationType { get; set; } = "Password";

  /// <summary>
  /// Restricts the set of accounts allowed to sign in. Applied on the sign in path only, so that
  /// accounts excluded here can still be managed. Use it to apply soft delete filters or
  /// application specific flags, eg. <c>q =&gt; q.IgnoreQueryFilters().WithoutDeleted().Where(p =&gt; p.IsManager)</c>.
  /// </summary>
  public Func<IQueryable<TUser>, IQueryable<TUser>>? SignInQuery { get; set; }

  /// <summary>
  /// Restricts the set of accounts visible to the management operations
  /// (register, change password, reset password).
  /// </summary>
  public Func<IQueryable<TUser>, IQueryable<TUser>>? LookupQuery { get; set; }

  /// <summary>
  /// Hook to add application specific claims to the identity built on sign in.
  /// </summary>
  public Action<ClaimsIdentity, TUser>? EnrichClaims { get; set; }
}
