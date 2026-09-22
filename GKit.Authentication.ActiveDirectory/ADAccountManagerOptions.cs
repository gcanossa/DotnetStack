namespace GKit.Authentication.ActiveDirectory;

public class ADAccountManagerOptions
{
  public string Host { get; set; } = "";

  /// <summary>
  /// Defaults to LDAPS. On non-Windows the bind uses <c>AuthType.Basic</c>, which sends the
  /// password in the clear — the previous 389/plaintext default made that the out-of-the-box
  /// behaviour.
  /// </summary>
  public int Port { get; set; } = 636;

  public bool IsSecure { get; set; } = true;

  public string Domain { get; set; } = "";
  public string QueryBase { get; set; } = "";

  /// <summary>
  /// Set to true only to permit an unencrypted Basic bind (test environments). Without it a
  /// cleartext-credential configuration fails fast instead of leaking passwords.
  /// </summary>
  public bool AllowUnencryptedCredentials { get; set; }
}
