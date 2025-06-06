using Microsoft.AspNetCore.Components;

namespace GKit.Authentication.Blazor;

public abstract class LoginComponentBase<T> : ComponentBase where T : new()
{
  [Inject]
  protected NavigationManager NavigationManager { get; set; } = default!;

  [SupplyParameterFromForm]
  public T Model { get; set; } = new();

  [SupplyParameterFromQuery]
  public string ReturnUrl { get; set; } = "/";

  public bool FailedLoginAttempt { get; protected set; } = false;

  protected async Task LoginUser()
  {
    var result = await SignInAsync(Model);

    if (result)
    {
      NavigationManager.NavigateTo(ReturnUrl);
    }
    else
    {
      FailedLoginAttempt = true;
    }
  }

  protected abstract Task<bool> SignInAsync(T credentials);

}