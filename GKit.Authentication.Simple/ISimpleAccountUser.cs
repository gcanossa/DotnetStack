namespace GKit.Authentication.Simple;

/// <summary>
/// Contract an application entity must fulfill in order to be used as an account source
/// by the <see cref="SimpleAccountManager{TContext,TUser}"/>.
/// </summary>
public interface ISimpleAccountUser
{
  /// <summary>
  /// Stable identifier of the user, mapped to the NameIdentifier claim.
  /// </summary>
  string Identifier { get; }

  /// <summary>
  /// Name used to log in. Expected to be unique.
  /// </summary>
  string AccountName { get; }

  /// <summary>
  /// Hash of the password, in the format produced by the configured <see cref="IPasswordHasher"/>.
  /// </summary>
  string? PasswordHash { get; set; }

  /// <summary>
  /// Mail address, mapped to the Email claim.
  /// </summary>
  string? Mail => null;

  /// <summary>
  /// Name shown to the user, mapped to the Name claim. Falls back to <see cref="AccountName"/>.
  /// </summary>
  string? DisplayName => null;

  /// <summary>
  /// Names mapped to the Role claims.
  /// </summary>
  IEnumerable<string> Roles => [];
}
