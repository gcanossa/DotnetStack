using System.Security.Cryptography;
using System.Text;

namespace GKit.Authentication.Simple;

/// <summary>
/// Unsalted, single round SHA512 hasher producing a lowercase hex string.
/// Provided for compatibility with existing databases only: register
/// <see cref="Pbkdf2PasswordHasher"/> for new applications.
/// </summary>
public class Sha512PasswordHasher : IPasswordHasher
{
  public string Hash(string password) =>
    Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();

  public bool Verify(string password, string? hash)
  {
    if (string.IsNullOrEmpty(hash)) return false;

    return CryptographicOperations.FixedTimeEquals(
      Encoding.UTF8.GetBytes(Hash(password)),
      Encoding.UTF8.GetBytes(hash.ToLowerInvariant()));
  }
}
