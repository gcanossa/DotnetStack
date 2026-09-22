using System.Security.Cryptography;

namespace GKit.Authentication.Simple;

/// <summary>
/// Default hasher. Produces salted PBKDF2 hashes in the format
/// <c>pbkdf2$sha256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>.
/// </summary>
public class Pbkdf2PasswordHasher(int iterations = Pbkdf2PasswordHasher.DefaultIterations) : IPasswordHasher
{
  public const int DefaultIterations = 210_000;

  private const string Prefix = "pbkdf2";
  private const int SaltSize = 16;
  private const int HashSize = 32;

  public string Hash(string password)
  {
    var salt = RandomNumberGenerator.GetBytes(SaltSize);
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

    return string.Join('$',
      Prefix,
      "sha256",
      iterations.ToString(),
      Convert.ToBase64String(salt),
      Convert.ToBase64String(hash));
  }

  public bool Verify(string password, string? hash)
  {
    if (string.IsNullOrEmpty(hash)) return false;

    var parts = hash.Split('$');
    if (parts.Length != 5 || parts[0] != Prefix) return false;
    if (!int.TryParse(parts[2], out var storedIterations) || storedIterations <= 0) return false;

    byte[] salt;
    byte[] expected;
    try
    {
      salt = Convert.FromBase64String(parts[3]);
      expected = Convert.FromBase64String(parts[4]);
    }
    catch (FormatException)
    {
      return false;
    }

    var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, storedIterations,
      new HashAlgorithmName(parts[1].ToUpperInvariant()), expected.Length);

    return CryptographicOperations.FixedTimeEquals(actual, expected);
  }
}
