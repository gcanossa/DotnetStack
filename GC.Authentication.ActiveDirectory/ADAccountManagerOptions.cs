namespace GC.Authentication.ActiveDirectory;

public class ADAccountManagerOptions
{
  public string Host { get; set; } = "";
  public int Port { get; set; } = 389;
  public bool IsSecure { get; set; } = false;
  public string Domain { get; set; } = "";
  public string QueryBase { get; set; } = "";
}