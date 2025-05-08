namespace GKit.Authentication.Blazor;

public class AuthenticationOptions
{
  public string LoginPath { get; set; } = "/account/login";
  public string AccessDeniedPath { get; set; } = "/account/access-denied";
}