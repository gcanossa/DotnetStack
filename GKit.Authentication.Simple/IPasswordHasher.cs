namespace GKit.Authentication.Simple;

/// <summary>
/// Strategy used to hash and verify the passwords stored in <see cref="ISimpleAccountUser.PasswordHash"/>.
/// </summary>
public interface IPasswordHasher
{
  /// <summary>
  /// Produces the value to store in <see cref="ISimpleAccountUser.PasswordHash"/>.
  /// </summary>
  string Hash(string password);

  /// <summary>
  /// Verifies a clear text password against a stored hash, in constant time whenever possible.
  /// </summary>
  bool Verify(string password, string? hash);
}
