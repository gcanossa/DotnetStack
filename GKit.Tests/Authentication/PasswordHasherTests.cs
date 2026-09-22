using GKit.Authentication.Simple;

namespace GKit.Tests.Authentication;

public class PasswordHasherTests
{
  [Fact]
  public void Pbkdf2RoundTrip()
  {
    var hasher = new Pbkdf2PasswordHasher(iterations: 1000);

    var hash = hasher.Hash("password");

    Assert.StartsWith("pbkdf2$sha256$1000$", hash);
    Assert.True(hasher.Verify("password", hash));
    Assert.False(hasher.Verify("Password", hash));
    Assert.False(hasher.Verify("", hash));
  }

  [Fact]
  public void Pbkdf2IsSalted()
  {
    var hasher = new Pbkdf2PasswordHasher(iterations: 1000);

    Assert.NotEqual(hasher.Hash("password"), hasher.Hash("password"));
  }

  [Fact]
  public void Pbkdf2RejectsMalformedHashes()
  {
    var hasher = new Pbkdf2PasswordHasher(iterations: 1000);

    Assert.False(hasher.Verify("password", null));
    Assert.False(hasher.Verify("password", ""));
    Assert.False(hasher.Verify("password", "not-a-hash"));
    Assert.False(hasher.Verify("password", "pbkdf2$sha256$x$AAAA$AAAA"));
    Assert.False(hasher.Verify("password", "pbkdf2$sha256$1000$not base64$AAAA"));
  }

  [Fact]
  public void Pbkdf2VerifiesAcrossIterationCounts()
  {
    var hash = new Pbkdf2PasswordHasher(iterations: 1000).Hash("password");

    // the stored iteration count wins, so raising the default does not lock existing users out
    Assert.True(new Pbkdf2PasswordHasher(iterations: 5000).Verify("password", hash));
  }

  [Fact]
  public void Sha512MatchesTheLegacyFormat()
  {
    var hasher = new Sha512PasswordHasher();

    // lowercase hex of a single unsalted SHA512 round, as stored by applications predating this library
    Assert.Equal(
      "887375daec62a9f02d32a63c9e14c7641a9a8a42e4fa8f6590eb928d9744b57b" +
      "b5057a1d227e4d40ef911ac030590bbce2bfdb78103ff0b79094cee8425601f5",
      hasher.Hash("Admin"));

    Assert.True(hasher.Verify("Admin", hasher.Hash("Admin")));
    Assert.False(hasher.Verify("admin", hasher.Hash("Admin")));
    Assert.False(hasher.Verify("Admin", null));
  }
}
